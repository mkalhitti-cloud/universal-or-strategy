# PTT-REPAIRS-02 Architecture Plan
**Status**: REVIEW_PASS_PENDING (Correction Cycle 1 — V1 JS-023 mis-citation fixed)
**Phase**: 1 (Architecture)
**Epic**: PTT-REPAIRS-02
**Author**: ptt-architect
**Date**: 2026-09-07

---

## Section 1: Context & Scope

### Epic Purpose

PTT-REPAIRS-02 addresses a single confirmed correctness defect in `CopyEngine.cs`:
`_liveEntryInstruments` is NOT cleared on leader order fill, causing all subsequent
`DispatchCopy` calls for the same instrument+direction to be silently blocked at Gate 5
check (a). The symptom is an alternating pattern:
- attempt 1 → leader + followers OK
- attempt 2 → leader only (followers MISSED)
- attempt 3 → leader + followers OK
- attempt 4 → leader only (followers MISSED)

Root cause is confirmed. Re-investigation is NOT required.

### Spec Items

| ID | File | Description |
|----|------|-------------|
| PTT-REPAIRS-02-T1 | `src/PropTraderTools/CopyEngine.cs` | Fix `_liveEntryInstruments` over-blocking on rapid re-entry after fill — clear instrKey in `EvictDedup` on `Filled` state |

### Deferred Backlog Carried From PTT-REPAIRS-01

| ID | Item | Priority | Status |
|----|------|----------|--------|
| DW-B24-01 | NT8-043 null-conditional unsubscription runtime crash confirmation | P2 | OPEN |
| DW-B24-02 | Manual E2E Ctrl+Shift+B runtime verify in live NT8 session | P1 | OPEN |
| DW-B24-03 | Skip-duplicate guard [Fact] for `if (acc == leader) continue` | P2 | OPEN |
| DW-B25-01 | Companion field race on `_pendingBeAccount` / `_pendingBeInstrument` plain refs | P3 | OPEN |
| DW-B26-01 | Reflection test upgrade Option B to Option A for `TradeCopierPanel_BeBufferBox_FieldDoesNotExist` | P2 | OPEN |
| DW-REPAIRS-01-01 | R5 Account.All constructor-path risk in BuildRuleRow | P2 | OPEN |
| DW-REPAIRS-01-02 | R2 PendingCancelCount non-volatile read in TryDrainWatchdog | P3 | OPEN |

Test count baseline at PTT-REPAIRS-01 close: **300 tests** (`[Fact]` count in `CopyEngineTests.cs`).
After PTT-REPAIRS-02: **300 + 1 = 301 tests**.

---

## Section 2: LANE-SPLIT GATE RESULT

### Gate Questions

**Q1. Same method or within 50 lines?**

The repair is a single change inside `EvictDedup` (lines 5762-5768, `CopyEngine.cs`). There is
no second candidate fix — Option B (more aggressive `ClearLiveEntryForInstrument` calls) is
ALREADY implemented in the codebase as DW-LB-FL-01 V7 Fix A (the `!hasPos` check at line 4191
is already hoisted above the `HasPosDedupChanged` dedup gate). Option B's premise is moot.

This is a single-method, single-block repair. Q1 = **YES** (single fix, single method, well under
50 lines). → **SINGLE-PIPELINE. STOP.**

**LANE-SPLIT GATE RESULT: SINGLE-PIPELINE**
(Q1 = YES — single change in `EvictDedup`; Options A/B reduce to a single valid fix.)

---

## Section 3: Architecture Decision

### Option Analysis

**Option A (selected)**: In `EvictDedup()`, on `Filled` state, also clear
`_liveEntryInstruments[filledInstrKey]` using the same TryRemove pattern already used for
the `Cancelled` branch.

**Option B (rejected — already moot)**: Call `ClearLiveEntryForInstrument` more aggressively
in `TryFirePositionState`. REJECTED because DW-LB-FL-01 V7 Fix A already hoists the
`TryClearLeaderDirectionOnFlat` call above the `HasPosDedupChanged` dedup gate (line 4191).
The clear fires regardless of dedup state. Option B's target scenario is already addressed.

### Why Option A Is Correct

The original comment at line 5765 ("Do NOT remove _liveEntryInstruments key -- trade is live")
was written for the MGC cancel+resubmit scenario where a second `OnOrderUpdate(Submitted)`
arrives for a resubmitted order BEFORE the fill. In that window, instrKey must stay set to
block the double-dispatch. However:

1. The MGC double-dispatch guard is ALREADY provided by `_entryDispatchedOrders` (DW-B91-A).
   Once an orderId is dispatched, `IsEntryDispatched(orderId)` blocks it at check (c) of
   `IsLiveEntryBlocked`, independent of instrKey.

2. The cancel+resubmit uses a NEW orderId for the resubmitted order. The instrKey guard for the
   ORIGINAL orderId is cleared on Cancelled (line 5758-5759, unchanged). The NEW orderId's
   dispatch is gated by the new instrKey TryAdd succeeding — which it will, because the original
   instrKey was cleared on Cancelled. This path is UNAFFECTED by Option A.

3. After a fill, the entry order lifecycle is COMPLETE. Followers are already dispatched
   (they dispatch on Submitted/Accepted — Gate 3 fires on `IsDispatchTriggerState`, which is
   Submitted/Accepted, well before the Filled event arrives). The instrKey guard has no
   protective function after the fill; it is only blocking legitimate re-entries.

4. The "authoritative cleanup" via `ClearLiveEntryForInstrument` on position-flat is now a
   SECONDARY guard (belt-and-suspenders). It handles the case where the Filled path somehow
   fails (defensive) and cleans up from any other lifecycle paths that might set instrKey.

### Fix Design: `EvictDedup` — Filled Block

**Method**: `internal void EvictDedup(string orderId, OrderState state)` — line 5740.
**File**: `src/PropTraderTools/CopyEngine.cs`.

**Current code (lines 5762-5768)**:
```csharp
if (state == OrderState.Filled)
{
    // DW-B142-MGC-02: clean up companion map (lazy).
    // Do NOT remove _liveEntryInstruments key -- trade is live.
    // PositionStateChanged flat gate (ClearLiveEntryForInstrument) is the authoritative cleanup.
    _entryInstrKeyByOrderId.TryRemove(orderId, out _);
}
```

**Fixed code (lines 5762-5770, replacing the above)**:
```csharp
if (state == OrderState.Filled)
{
    // PTT-REPAIRS-02: clear instrKey on fill -- entry lifecycle is complete, followers dispatched.
    // Pattern mirrors Cancelled branch. ClearLiveEntryForInstrument remains as secondary guard.
    // MGC cancel+resubmit guard is provided by _entryDispatchedOrders (DW-B91-A) -- not instrKey.
    if (_entryInstrKeyByOrderId.TryRemove(orderId, out var filledInstrKey))
        _liveEntryInstruments.TryRemove(filledInstrKey, out _);
}
```

**Change summary**:
- Lines 5764-5765: Remove old comment ("Do NOT remove... trade is live").
- Line 5766: Remove old comment ("PositionStateChanged flat gate is the authoritative cleanup").
- Line 5767: Change `_entryInstrKeyByOrderId.TryRemove(orderId, out _);` to
  `if (_entryInstrKeyByOrderId.TryRemove(orderId, out var filledInstrKey)) _liveEntryInstruments.TryRemove(filledInstrKey, out _);`.
- Line 5738: Update header comment: `// PTT-REPAIRS-02: CYC=6: terminal-guard(1) + Cancelled(2) + instrKey-lookup(3) + Filled(4) + filledInstrKey-TryRemove(5).`
  (NOTE: the CYC comment counts +1 for the new `if (TryRemove)` branch inside the Filled block.)

**No other method changes required.**

---

## Section 4: CYC Verification

| Method | File | CYC Before | CYC After | Budget | Status |
|--------|------|-----------|-----------|--------|--------|
| `EvictDedup` | `CopyEngine.cs:5740` | 5 | 6 | ≤7 | PASS |
| `IsLiveEntryBlocked` | `CopyEngine.cs:5710` | 4 | 4 (UNCHANGED) | ≤5 | PASS |
| `ClearLiveEntryForInstrument` | `CopyEngine.cs:5726` | 2 | 2 (UNCHANGED) | ≤8 | PASS |
| `TryClearLeaderDirectionOnFlat` | `CopyEngine.cs:4230` | 4 | 4 (UNCHANGED) | ≤8 | PASS |

**CYC change detail for `EvictDedup`**:

Branch count after fix:
1. Compound terminal guard `if (state != Filled && state != Cancelled && state != Rejected)` = 1
2. `if (state == OrderState.Cancelled)` = 1
3. `if (_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey))` = 1
4. `if (state == OrderState.Filled)` = 1
5. `if (_entryInstrKeyByOrderId.TryRemove(orderId, out var filledInstrKey))` = 1 **(NEW)**
Total: base(1) + 5 branches = CYC=6. PASS (≤7 budget).

---

## Section 5: MGC Cancel+Resubmit Guard Integrity

The DW-B142-MGC-02 guard must remain intact after the fix. Analysis:

| Scenario | Before Fix | After Fix | Verdict |
|----------|------------|-----------|---------|
| First dispatch: instrKey set in `_liveEntryInstruments` | YES | YES (unchanged) | OK |
| MGC cancel: `EvictDedup(Cancelled)` clears instrKey via `_entryInstrKeyByOrderId` | YES | YES (unchanged) | OK |
| MGC resubmit NEW orderId: instrKey clear → TryAdd succeeds → dispatched | YES | YES (unchanged) | OK |
| MGC resubmit BEFORE cancel: instrKey still set → blocked | YES | YES (Cancelled not Filled path) | OK |
| Leader fill: instrKey cleared by new fix | NO (stuck) | YES (released) | FIXED |
| Position flat: `ClearLiveEntryForInstrument` clears instrKey | YES | YES (now secondary guard) | OK |

The fix does not alter the Cancelled path at lines 5751-5760. The MGC guard is fully intact.

---

## Section 6: Data Flow Summary

```
OnOrderUpdate(Submitted/Accepted)
  └─► DispatchCopy
        └─► IsLiveEntryBlocked(instrKey, orderId, price)
              (a) _liveEntryInstruments.ContainsKey(instrKey)  ← was stuck here on attempt 2
              (b) IsDedup(orderId, price)
              (c) IsEntryDispatched(orderId)
              → false: TryAdd instrKey to _liveEntryInstruments
                       TryAdd orderId→instrKey to _entryInstrKeyByOrderId
              → Followers dispatched

OnOrderUpdate(Filled)  ← FIXED HERE
  └─► EvictDedup(orderId, Filled)
        _dedupCache.TryRemove(orderId, out _)
        if (_entryInstrKeyByOrderId.TryRemove(orderId, out var filledInstrKey))
            _liveEntryInstruments.TryRemove(filledInstrKey, out _)  ← NEW: releases instrKey
        [_entryDispatchedOrders NOT cleared here -- handled by TryEvictFollowerBeSlot]

OnOrderUpdate(Filled) [next trade, same instrument+direction]
  └─► DispatchCopy
        └─► IsLiveEntryBlocked(instrKey, orderId2, price2)
              (a) _liveEntryInstruments.ContainsKey(instrKey) → false (cleared above) ✓
              → Followers dispatched ✓
```

---

## Section 7: Test Architecture

### Framework & Seam

- Framework: xUnit. `[Fact]` only.
- Seam: `InternalsVisibleTo("PropTraderTools.Tests")` declared at `CopyEngine.cs` line 46.
- All four required test seam methods already exist at lines 4264-4282. No new seam methods needed.

**Existing seams used**:

| Seam | Line | Signature | Used By Test |
|------|------|-----------|-------------|
| `IsLiveEntryBlocked_ForTest` | 4264 | `internal bool (string instrKey, string orderId, double limitPrice)` | Steps 1+5 |
| `EvictDedup_ForTest` | 4270 | `internal void (string orderId, NinjaTrader.Cbi.OrderState state)` | Step 3 |
| `LiveEntryInstrumentsContains_ForTest` | 4276 | `internal bool (string key)` | Steps 2+4 |
| `EntryInstrKeyByOrderIdContains_ForTest` | 4279 | `internal bool (string orderId)` | (Optional: Step 3b) |

### Test: `IsLiveEntryBlocked_ClearsOnFill_AllowsReentry`

**Name**: `IsLiveEntryBlocked_ClearsOnFill_AllowsReentry`
**Spec item**: PTT-REPAIRS-02-T1
**Test class**: `CopyEngineTests` (append to `CopyEngineTests.cs` after T_R6)
**Scenario**: First dispatch sets `_liveEntryInstruments[instrKey]`. Fill event clears it (via fixed
`EvictDedup`). Second `DispatchCopy` for same instrKey passes Gate 5 check (a).

**Implementation**:
```csharp
// PTT-REPAIRS-02 T1: verify _liveEntryInstruments is cleared on Filled,
// allowing a second DispatchCopy for the same instrKey to pass Gate 5 check (a).
// Uses InternalsVisibleTo seams declared at CopyEngine.cs:46.
// No NT8 type construction required -- seams operate on string keys and OrderState enum.
[Fact]
public void IsLiveEntryBlocked_ClearsOnFill_AllowsReentry()
{
    const string instrKey = "MGC DEC26|Sell";
    const string orderId1 = "PTTR02-orderId-1";
    const string orderId2 = "PTTR02-orderId-2";
    const double limitPrice = 0.0;

    // Pre-condition: clear any residual state from other tests
    _engine.ClearLiveEntryForInstrument_ForTest("MGC DEC26");

    // Step 1: First dispatch -- Gate 5 should pass (instrKey not yet set)
    bool blocked1 = _engine.IsLiveEntryBlocked_ForTest(instrKey, orderId1, limitPrice);
    Assert.False(blocked1); // first dispatch must proceed

    // Step 2: instrKey is now set in _liveEntryInstruments
    Assert.True(_engine.LiveEntryInstrumentsContains_ForTest(instrKey));

    // Step 3: Leader order fills -- EvictDedup must clear instrKey (the fix)
    _engine.EvictDedup_ForTest(orderId1, NinjaTrader.Cbi.OrderState.Filled);

    // Step 4: instrKey must be cleared from _liveEntryInstruments after the fill
    Assert.False(_engine.LiveEntryInstrumentsContains_ForTest(instrKey));

    // Step 5: Second dispatch for same instrKey -- Gate 5 check (a) must pass
    bool blocked2 = _engine.IsLiveEntryBlocked_ForTest(instrKey, orderId2, limitPrice);
    Assert.False(blocked2); // second dispatch must proceed (was blocked before fix)
}
```

**Assertions**:
1. `blocked1 == false` — first dispatch gate passes (baseline).
2. `LiveEntryInstrumentsContains(instrKey) == true` — confirms instrKey was set after first dispatch.
3. `EvictDedup(orderId1, Filled)` called — simulates leader order fill.
4. `LiveEntryInstrumentsContains(instrKey) == false` — confirms instrKey was cleared by the fix.
5. `blocked2 == false` — second dispatch gate passes (the defect is fixed).

**CYC**: Test method CYC=1 (no branches). PASS.
**JS compliance**: No lock(). No DateTime.Now. ASCII-only strings. PASS.

---

## Section 8: 7-Scan Contract

### SCAN-01: CYC ≤ 8 (all changed methods)

| Method | CYC Before | CYC After | Budget | Status |
|--------|-----------|-----------|--------|--------|
| `EvictDedup` | 5 | 6 | ≤7 | PASS |
| `IsLiveEntryBlocked` | 4 | 4 (unchanged) | ≤5 | PASS |

### SCAN-02: No `lock()` in changed files

- `EvictDedup`: no lock() introduced. ConcurrentDictionary.TryRemove is lock-free (JS-025). PASS.

### SCAN-03: No `return null` in changed methods

- `EvictDedup`: `void` return. PASS.

### SCAN-04: No `DateTime.Now` (use `DateTime.UtcNow`)

- No DateTime usage introduced. PASS.

### SCAN-05: No `async void` non-event-handler

- No async methods introduced. PASS.

### SCAN-06: ASCII-only string literals

- New comment text: all ASCII. Log strings: not introduced. PASS.

### SCAN-07: No `?.Event -=` null-conditional unsubscription

- No event subscription changes. PASS.

---

## Section 9: Ticket Summary

### Ticket PTT-REPAIRS-02-T1

**Spec item**: PTT-REPAIRS-02-T1
**File**: `src/PropTraderTools/CopyEngine.cs`
**Test file**: `src/PropTraderTools/CopyEngineTests.cs`

**Method to modify**: `internal void EvictDedup(string orderId, OrderState state)` (line 5740)

**Change**: Replace lines 5763-5768 (the `Filled` block body):

BEFORE (lines 5763-5768):
```csharp
    // DW-B142-MGC-02: clean up companion map (lazy).
    // Do NOT remove _liveEntryInstruments key -- trade is live.
    // PositionStateChanged flat gate (ClearLiveEntryForInstrument) is the authoritative cleanup.
    _entryInstrKeyByOrderId.TryRemove(orderId, out _);
```

AFTER:
```csharp
    // PTT-REPAIRS-02: clear instrKey on fill -- entry lifecycle complete, followers dispatched.
    // Mirrors Cancelled branch. ClearLiveEntryForInstrument remains as secondary guard.
    // MGC cancel+resubmit guard provided by _entryDispatchedOrders (DW-B91-A) -- not instrKey.
    if (_entryInstrKeyByOrderId.TryRemove(orderId, out var filledInstrKey))
        _liveEntryInstruments.TryRemove(filledInstrKey, out _);
```

**Header comment to update** (line 5738):
```
// PTT-REPAIRS-02: CYC=6: terminal-guard(1) + Cancelled(2) + instrKey-lookup(3) + Filled(4) + filledInstrKey-remove(5).
```
(NOTE: Replaces the DW-B142-MGC-02 CYC=5 comment.)

**Test to add** (append to `CopyEngineTests.cs` after T_R6 block):
- Name: `IsLiveEntryBlocked_ClearsOnFill_AllowsReentry`
- Seams used: `IsLiveEntryBlocked_ForTest`, `EvictDedup_ForTest`, `LiveEntryInstrumentsContains_ForTest`, `ClearLiveEntryForInstrument_ForTest`
- Full implementation in Section 7 above.

**[Fact] count after this ticket**: 301 (was 300).

---

## Section 10: NT8 API Surface Summary

| API | Availability | Confirmed In | Used By Fix |
|-----|-------------|-------------|-------------|
| `ConcurrentDictionary<string,string>.TryRemove(string, out string)` | .NET std | CopyEngine.cs:5758 (Cancelled branch) | Fix mirrors this exact call |
| `ConcurrentDictionary<string,byte>.TryRemove(string, out byte)` | .NET std | CopyEngine.cs:5759 | Fix uses this on `_liveEntryInstruments` |
| `OrderState.Filled` | NT8 enum (NinjaTrader.Cbi) | CopyEngine.cs:5762 | Fix is inside existing `Filled` branch |
| `IsLiveEntryBlocked_ForTest` | internal seam (L46 IVT) | CopyEngine.cs:4264 | Test step 1+5 |
| `EvictDedup_ForTest` | internal seam (L46 IVT) | CopyEngine.cs:4270 | Test step 3 |
| `LiveEntryInstrumentsContains_ForTest` | internal seam (L46 IVT) | CopyEngine.cs:4276 | Test step 2+4 |
| `ClearLiveEntryForInstrument_ForTest` | internal seam (L46 IVT) | CopyEngine.cs:4273 | Test pre-condition |

All APIs confirmed from source code. No phantom APIs.

---

## Section 11: RULES_CATALOG Compliance Summary

| Rule | Description | Status |
|------|-------------|--------|
| JS-001 | No throw in hot paths | PASS — `TryRemove` does not throw; no try/catch needed |
| JS-002 | No return null | PASS — `EvictDedup` is void |
| JS-021 | No `lock()` | PASS — zero lock() introduced; ConcurrentDictionary.TryRemove is lock-free |
| JS-008 | No mutable struct fields / unfrozen brushes | PASS — no struct usage introduced |
| JS-023 | No UI update from off-thread without Dispatcher.InvokeAsync | N/A — no UI changes in this fix |
| JS-025 | ConcurrentDictionary is lock-free canonical | PASS — TryRemove pattern mirrors existing Cancelled branch |
| JS-066 | CYC ≤ 8 | PASS — EvictDedup: 5→6 (≤7 budget); IsLiveEntryBlocked: 4 (unchanged, ≤5 budget) |
| JS-080 (implied) | ASCII-only string literals | PASS — all comment text and strings ASCII |

---

## Section 12: Deferred Work

### Items Carried Forward (All Unchanged)

| ID | Item | Priority | Target | Status |
|----|------|----------|--------|--------|
| DW-B24-01 | NT8-043 null-conditional unsubscription runtime crash confirmation | P2 | B27+ | OPEN |
| DW-B24-02 | Manual E2E Ctrl+Shift+B runtime verify | P1 | ASAP post-merge | OPEN |
| DW-B24-03 | Skip-duplicate guard [Fact] | P2 | B27+ | OPEN |
| DW-B25-01 | Companion field race on plain singleton refs | P3 | B28+ | OPEN |
| DW-B26-01 | Reflection test upgrade Option B to Option A | P2 | B28+ | OPEN |
| DW-REPAIRS-01-01 | R5 Account.All constructor-path risk | P2 | B28+ | OPEN |
| DW-REPAIRS-01-02 | R2 PendingCancelCount non-volatile read | P3 | Future | OPEN |

### New Deferred Items From PTT-REPAIRS-02 Analysis

| ID | Item | Priority | Target | Status |
|----|------|----------|--------|--------|
| DW-REPAIRS-02-01 | `_entryDispatchedOrders` is NOT cleared in the Filled branch of `EvictDedup`. Comment at line 5769 says "Filled/Rejected _entryDispatchedOrders eviction handled in TryEvictFollowerBeSlot." If `TryEvictFollowerBeSlot` does not fire (e.g., no BE slot), the orderId lingers. This pre-exists the current fix and is not part of this scope. However, if rapid re-entry with the SAME orderId (unlikely but possible with certain brokers) occurs, `IsEntryDispatched(orderId)` would block it. Recommend: runtime E2E verification that orderId is released correctly across the full fill lifecycle. | P2 | B28+ | OPEN |

---

*ptt-architect · PTT-REPAIRS-02 · 2026-09-07*
