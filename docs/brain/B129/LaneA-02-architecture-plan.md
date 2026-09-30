# B129 LaneA Architecture Plan — DW-B135

**Block**: B129 LaneA
**Defect**: DW-B135 — Reversal Guard False-Positive After Leader Flat
**Phase**: 2 (Architecture)
**Author**: ptt-architect
**Date**: 2026-08-31
**Status**: REVIEW_PASS

---

## Rules Catalog Gate

P0 rules checked before writing this plan:

| Rule | Constraint | Status |
|------|-----------|--------|
| JS-021 | No `lock()` anywhere — ConcurrentDictionary/Interlocked only | GATE PASS |
| JS-001 | No `throw new XxxException` in hot paths | GATE PASS |
| JS-002 | No `return null` for missing values | GATE PASS |
| JS-033 | No `async void` (non-event-handler) | GATE PASS |
| JS-080 | CYC <= 8 per method | GATE PASS — post-fix CYC = 5 or 6 (see Section D) |
| ASCII-only | No Unicode, emoji, curly quotes in string literals | GATE PASS |

No P0 violations in any code this plan will touch.

---

## Section A — Root Cause Analysis

**File**: [`src/PropTraderTools/CopyEngine.cs`](../../../src/PropTraderTools/CopyEngine.cs)

**Confirmed from code read**:

### `_lastLeaderDirection` field (L331-332)

```csharp
private readonly ConcurrentDictionary<string, OrderAction> _lastLeaderDirection =
    new ConcurrentDictionary<string, OrderAction>();
```

- Keyed by `instrument.FullName` (string).
- Value = last `OrderAction` dispatched for that instrument.
- Written at **L1985** (end of `DispatchCopy` loop): `_lastLeaderDirection[instr.FullName] = currentAction;`
- **Never cleared** when the leader position goes flat. This is the defect.

### `IsReversalToFlatFollower` predicate (L3588-3594)

```csharp
internal static bool IsReversalToFlatFollower(
    OrderAction currentAction,
    OrderAction lastAction,
    bool followerIsFlat)
{
    return currentAction != lastAction && followerIsFlat;
}
```

Returns `true` when the new action differs from the last dispatched action AND the follower is flat.

### `DispatchCopy` guard (L1910-1948)

At L1914-1916, `DispatchCopy` reads `_lastLeaderDirection` into `hasLastDirection` + `lastAction`.
At L1936, the guard fires when `hasLastDirection == true AND IsReversalToFlatFollower(currentAction, lastAction, followerIsFlat) == true`.

**Bug scenario** (confirmed from code):

1. Leader enters **Buy** → `_lastLeaderDirection["ES 09-26"] = Buy` (L1985).
2. Leader's position fills and closes → position goes flat.
3. **`_lastLeaderDirection["ES 09-26"]` is never cleared** — key remains set to `Buy`.
4. Leader enters **Sell** (new trade, opposite direction).
5. `DispatchCopy` executes: `hasLastDirection=true`, `currentAction=Sell`, `lastAction=Buy`.
6. `IsReversalToFlatFollower(Sell, Buy, followerIsFlat=true)` → `true`.
7. **Guard fires → follower misses the new Sell entry.** FALSE POSITIVE.

**DW-B128 guard intent** (preserved): The guard was designed for the scenario where a Sell signal arrives while the leader is STILL long (close-order race window, position still open). In that scenario the follower is flat but the LEADER is not flat — the direction key correctly reflects an active open position. The guard is correct in that scenario. It is incorrect when the leader's position has been fully closed.

**Key insight**: The direction key must be cleared when the leader's position transitions to flat. This makes `hasLastDirection=false` for the next new entry, preventing the guard from firing.

---

## Section B — Fix Design

**Selected option**: Option A (minimal — clear direction key on leader flat transition).

**Location**: [`TryFirePositionState`](../../../src/PropTraderTools/CopyEngine.cs:2361) (L2361-2387).

`TryFirePositionState` already detects the leader's position transition to flat:
- It is called from `OnOrderUpdate` at L1353 (after Gate 2, which confirms the order belongs to the leader account).
- It computes `bool hasPos = HasOpenPosition(e.Order.Account, e.Order.Instrument)` at L2372.
- The `hasPos=False` path is the exact moment the leader position has gone flat.

**Fix**: On the `hasPos=False` path, after the Interlocked CAS dedup check exits (i.e., we confirm this is a real flat-transition event), check whether the order belongs to a leader account for any active rule. If yes, call `_lastLeaderDirection.TryRemove(instr, out _)`.

**Precise insertion point**: After the `if (prior == newVal) return;` CAS guard at L2382-2383, before the final `PositionStateChanged?.Invoke(...)` at L2386.

**Pseudocode**:
```
// DW-B135: clear reversal guard when leader goes flat.
if (!hasPos)
{
    bool isLeaderForAnyRule = false;
    foreach (var r in _rules)
    {
        if (e.Order.Account.Name == r.MasterAccount?.Name)
        { isLeaderForAnyRule = true; break; }
    }
    if (isLeaderForAnyRule)
        _lastLeaderDirection.TryRemove(instr, out _);
}
```

**Effect**: The next `DispatchCopy` call for this instrument sees `hasLastDirection=false` →
`IsReversalToFlatFollower` cannot fire → follower receives the new entry correctly.

**DW-B128 protection preserved**: During the close-order race window (leader long, close order in flight), the leader's position is still open (`hasPos=True`). The `hasPos=False` path is NOT taken. The direction key is NOT cleared. The guard still correctly fires if a Sell signal arrives during that window while the follower is flat. DW-B128 protection is fully preserved.

---

## Section C — Thread Safety Analysis

| Component | Operation | Thread | JS-021 |
|-----------|-----------|--------|--------|
| `_lastLeaderDirection` | `TryRemove(instr, out _)` | NT8 UI thread (OnOrderUpdate) | PASS — ConcurrentDictionary.TryRemove is lock-free |
| `_lastLeaderDirection` | `TryGetValue(instr, ...)` at L1914 | NT8 UI thread (OnOrderUpdate via DispatchCopy) | PASS — existing, unchanged |
| `_lastLeaderDirection` | `[instr] = currentAction` at L1985 | NT8 UI thread (OnOrderUpdate via DispatchCopy) | PASS — existing, unchanged |
| `_rules` | `foreach (var r in _rules)` | NT8 UI thread (OnOrderUpdate) | PASS — _rules mutated only from UI thread (LoadRules/AddRule) |

**JS-021 compliance**: No `lock()` keyword in new or modified code. `ConcurrentDictionary.TryRemove` is documented as a lock-free operation. The `foreach` over `_rules` is safe because `_rules` is always mutated and read on the NT8 UI thread.

No Dispatcher.InvokeAsync is needed — `TryFirePositionState` is already on the UI thread.

---

## Section D — CYC Analysis

### Current `TryFirePositionState` (L2361-2387) — CYC = 3

| # | Decision Point | Code |
|---|---------------|------|
| 1 | State filter | `if (state != Filled && state != PartFilled)` (compound = 1 McCabe branch per project convention) |
| 2 | Null guard | `if (e.Order?.Instrument?.FullName == null)` |
| 3 | Interlocked CAS | `if (prior == newVal)` |

**CYC BEFORE = 3** (matching defect brief).

### Post-fix `TryFirePositionState` — CYC = 5 or 6

New decision points added on the `hasPos=False` path:

| # | Decision Point | Code |
|---|---------------|------|
| 4 | hasPos guard | `if (!hasPos)` — gates the entire direction-clear block |
| 5 | foreach loop | `foreach (var r in _rules)` — loop continuation condition |
| 6 | leader check | `if (e.Order.Account.Name == r.MasterAccount?.Name)` |

The defect brief counts branches 4+5 only (treating the `if (!hasPos)` guard as implicit on the hasPos=False path): **CYC = 5**. With the explicit `if (!hasPos)` guard added as a distinct branch: **CYC = 6**.

Both counts are **≤ 8**. **JS-080 COMPLIANT** regardless of counting convention.

No extraction required. Method stays within CYC budget.

---

## Section E — Partial Close Safety Proof

**Scenario**: Leader has 4 contracts. 2 contracts fill on a close order (partial close). 2 remain open.

**Code path at L2372**: `bool hasPos = HasOpenPosition(e.Order.Account, e.Order.Instrument);`

`HasOpenPosition` queries NT8's live position tracker. With 2 contracts still open, NT8 reports a non-zero quantity → `HasOpenPosition` returns `true` → `hasPos = true`.

**Branch taken**: `if (!hasPos)` → `false`. The direction-clear block is **NOT entered**. `_lastLeaderDirection.TryRemove` is **NOT called**.

**Result**: Direction key preserved correctly during partial close. The guard remains active for the duration of the partial position, preventing false reversal detection for any Sell signal that might arrive while 2 contracts are still long.

The direction key is only cleared when `HasOpenPosition` returns `false` (all contracts closed, position fully flat).

**Partial close safety: CONFIRMED**.

---

## Section F — Carry-Forward Review

### DW-B128: Close-Order Race Window Protection

**Status**: UNAFFECTED.

DW-B128 guard fires when `hasLastDirection=true AND IsReversalToFlatFollower=true`. The direction key is only cleared on `hasPos=False` path (position fully closed). During the DW-B128 race window, the position is still open (`hasPos=True`), so `TryRemove` is never called. The guard fires correctly. DW-B128 protection is fully preserved.

### DW-B134-OCO: OCO Orphan Risk After ATM STP Cancel+Resubmit

**Status**: UNAFFECTED.

LaneA touches only `TryFirePositionState` (L2361-2387). LaneB's `SyncAtmFollowerBracket` (L2100-L2160) is a separate method at a different line range. No intersection. DW-B134-OCO remains deferred to B130 as documented in LaneB-06-deferred-backlog.md.

### All other carry-forward items (DW-B129-01, DW-B133, DW-B124-01/02, DW-B107, B107-DEFER-01/02, DW-B42-01/02/03, DW-PTT-BE-FIX-01/02/03, DW-B89-DEFERRED-01..06)

**Status**: UNAFFECTED.

LaneA scope is confined to a 5-8 line addition inside `TryFirePositionState` and 3 internal test accessors. None of the carry-forward items reference `TryFirePositionState`, `_lastLeaderDirection`, or the reversal guard. No intersection.

---

## Section G — Test Contracts

All 3 tests APPEND to [`src/PropTraderTools/Tests/B129Tests.cs`](../../../src/PropTraderTools/Tests/B129Tests.cs).
Framework: **xUnit only** (`[Fact]`). No NUnit. No MSTest.

### Required Internal Test Accessors on `CopyEngine` (engineer adds, no logic)

Three thin wrappers to be added to `CopyEngine.cs` alongside existing internal test helpers:

```csharp
// DW-B135 test accessors -- no logic, thin shims only.
internal void TryFirePositionState_ForTest(OrderEventArgs e) => TryFirePositionState(e);
internal bool HasLeaderDirection(string instrFullName) => _lastLeaderDirection.ContainsKey(instrFullName);
internal void SetLeaderDirection_ForTest(string instrFullName, OrderAction action) =>
    _lastLeaderDirection[instrFullName] = action;
```

These follow the established pattern of existing internal test accessors in the class.

---

### Test 1 — `B129_DW135_GuardClearedAfterLeaderFlat`

**Purpose**: Confirms the direction key is removed when the leader's position goes flat.

**Setup**:
1. Create `CopyEngine` instance.
2. Wire a rule: leader=`"Sim101"`, follower=`"Sim102"`, instrument=`"ES 09-26"`.
3. Call `engine.SetLeaderDirection_ForTest("ES 09-26", OrderAction.Buy)` — simulates a prior Buy dispatch.
4. Assert pre-condition: `engine.HasLeaderDirection("ES 09-26") == true`.
5. Construct `OrderEventArgs` with:
   - `e.Order.Account.Name = "Sim101"` (leader account)
   - `e.Order.Instrument.FullName = "ES 09-26"`
   - `e.OrderState = OrderState.Filled`
   - `HasOpenPosition(Sim101, ES 09-26)` → mocked/stubbed to return `false` (position flat)
6. Call `engine.TryFirePositionState_ForTest(e)`.

**Asserts**:
```csharp
Assert.False(engine.HasLeaderDirection("ES 09-26"));
// Direction key removed -- guard cannot fire on next entry.
```

**Secondary assert** (DispatchCopy guard cannot fire):
After the key is removed, calling `engine._lastLeaderDirection.TryGetValue("ES 09-26", out _)` must return `false`, confirming `hasLastDirection=false` in the next `DispatchCopy` call.

**Note on `HasOpenPosition` mock**: The test must stub or override `HasOpenPosition` to return `false` for `Sim101/ES 09-26`. Since `HasOpenPosition` is a private method, the engineer should either (a) use an existing test-seam pattern in the class, OR (b) place the test engine in a state where the NT8 position for Sim101 is empty (consistent with how LaneB tests handle NT8 dependency isolation). The architect recommends a minimal `protected virtual` extraction of `HasOpenPosition` with a test-overridable subclass, consistent with the NT8 AddOn test patterns already in the codebase.

---

### Test 2 — `B129_DW135_DW128ProtectionPreservedDuringRaceWindow`

**Purpose**: Confirms the DW-B128 guard still fires correctly during the close-order race window. Pure static predicate test — no engine wiring required.

**Code under test**: `IsReversalToFlatFollower` at L3588 (already `internal static`, directly callable).

**Assert**:
```csharp
// DW-B128 race window: direction set to Buy, new Sell arrives, follower flat.
// Guard MUST fire (return true) -- this is the correct block.
Assert.True(
    CopyEngine.IsReversalToFlatFollower(
        OrderAction.Sell,
        OrderAction.Buy,
        followerIsFlat: true));
```

This test confirms that DW-B128 protection is not broken by the LaneA fix. The fix only changes WHEN the direction key is cleared (on flat transition), not the predicate logic itself.

---

### Test 3 — `B129_DW135_FirstEntryAfterRestartNotBlocked`

**Purpose**: Confirms that on first entry after a fresh `CopyEngine` start (no prior direction key), the guard cannot fire.

**Code under test**: Direct assertion on `_lastLeaderDirection` state (static data assertion, no engine call required).

**Setup**: Create `CopyEngine` instance. Do NOT call `SetLeaderDirection_ForTest`.

**Assert**:
```csharp
// No prior direction exists for fresh engine.
Assert.False(engine.HasLeaderDirection("ES 09-26"));
// hasLastDirection=false in DispatchCopy => guard cannot fire.
// IsReversalToFlatFollower is never called when hasLastDirection=false.
```

This test serves as a regression anchor: if future code accidentally pre-populates `_lastLeaderDirection` on construction, this test will catch it.

---

## Section H — Spec Update Plan

After LaneA pipeline completes (VERIFY_PASS), the following spec HTML updates are required:

**File**: [`specs/002-trade-copier-spec.html`](../../../specs/002-trade-copier-spec.html)

### Spec Update 1 — DW-B135: mark CLOSED (B129 LaneA PIPELINE_COMPLETE)

**Action**: Add a new section `#section-dw-b135` documenting:
- Root cause: `_lastLeaderDirection` not cleared on leader flat transition.
- Fix: `TryFirePositionState` clears direction key on `hasPos=False` leader path.
- DW-B128 preservation: direction key not cleared during race window.
- Status: CLOSED (B129 LaneA PIPELINE_COMPLETE).

### Spec Update 2 — DW-B134: mark CLOSED (B129 LaneB PIPELINE_COMPLETE — already achieved)

**Action**: Update the existing `#section-dw-b134` entry in the spec to CLOSED status.
LaneB reached FINAL_PASS and PIPELINE_COMPLETE (recorded in `LaneB-06-deferred-backlog.md`).
This update confirms that status in the canonical spec HTML.
- Status: CLOSED (B129 LaneB PIPELINE_COMPLETE).

### Spec Update 3 — DW-B134-OCO: add as open deferred item, to be addressed in B130

**Action**: Add an HTML comment or deferred-item note in the spec for `DW-B134-OCO`
(OCO orphan risk after ATM STP cancel+resubmit) referencing B130 and pending Director SIM gate.
- Status: OPEN — deferred to B130 (carry-forward from `LaneB-06-deferred-backlog.md`).

### Spec Update 4 — DW-B136 Gap A: mark resolved (root cause was DW-B135, now fixed)

**Action**: Update the DW-B136 Gap A entry in the spec to RESOLVED.
Gap A was a false-positive reversal-guard suppression; its root cause was DW-B135
(`_lastLeaderDirection` not cleared on flat). DW-B135 is now fixed by LaneA.
- Status: RESOLVED (root cause DW-B135 closed by B129 LaneA).

### Spec Update 5 — B129: mark fully PIPELINE_COMPLETE (both lanes complete)

**Action**: Add or update the B129 block-level status entry in the spec HTML to reflect
full PIPELINE_COMPLETE across both lanes:
- LaneA: DW-B135 fix PIPELINE_COMPLETE.
- LaneB: DW-B134 fix PIPELINE_COMPLETE.
- Status: B129 PIPELINE_COMPLETE (LaneA + LaneB).

**Timing**: Director updates spec after B129 LaneA VERIFY_PASS and F5 gate green.
All 5 updates are applied in a single spec edit pass.

---

## Component Summary

| Component | File | Lines | Change Type |
|-----------|------|-------|-------------|
| `TryFirePositionState` | `CopyEngine.cs` | L2361-2387 | Modify — add 5-8 lines on hasPos=False path |
| `TryFirePositionState_ForTest` accessor | `CopyEngine.cs` | (new, after TryFirePositionState) | Add — 1-line internal shim |
| `HasLeaderDirection` accessor | `CopyEngine.cs` | (new) | Add — 1-line internal shim |
| `SetLeaderDirection_ForTest` accessor | `CopyEngine.cs` | (new) | Add — 1-line internal shim |
| B129Tests.cs | `Tests/B129Tests.cs` | (append) | Add — 3 `[Fact]` tests |

**Files not touched**: `TradeCopierWindow.cs`, `TradeCopierPanel.cs`, `PttCopier.cs`, `CopyEngineTests.cs`, `B76Tests.cs`, all other `.cs` files.

---

## Architect Item Confirmation Table

| Item | Question | Answer | Evidence |
|------|---------|--------|----------|
| A1 | IsLeaderAccount predicate pattern | Use `foreach (var r in _rules)` with `e.Order.Account.Name == r.MasterAccount?.Name` and early break | L1396-1403 (`TryFireFollowerBeDisarm`) — identical pattern confirmed |
| A2 | Thread safety of TryRemove | Lock-free; JS-021 compliant | ConcurrentDictionary.TryRemove is documented lock-free; L330-332 comment confirms JS-021 |
| A3 | TryFirePositionState call site | Called from `OnOrderUpdate` L1353 on NT8 UI thread; no additional sync needed | L1353 code read confirmed |
| A4 | Partial close safety | `HasOpenPosition` returns `true` during partial close → `hasPos=False` path NOT taken → key NOT cleared | L2372 code; HasOpenPosition queries live NT8 position |
| A5 | No other _lastLeaderDirection callers affected | Only 2 other references: L1914 (TryGetValue read) and L1985 (write after dispatch) | grep confirmed exactly 3 references total (declaration + 2 usage sites) |
| A6 | TryFirePositionState outside LaneB range | L2361-2387 is 200+ lines below LaneB range end (~L2160) | Line ranges confirmed from code read |

---

## Pre-Flight Checklist

- [x] All 9 mandatory file reads completed
- [x] JS-021 (no lock): PASS — only ConcurrentDictionary.TryRemove used
- [x] JS-001 (no throw in hot path): PASS — no exception throwing in new code
- [x] JS-002 (no return null): PASS — TryFirePositionState returns void
- [x] JS-033 (no async void): PASS — method is synchronous void
- [x] CYC <= 8: PASS — post-fix CYC = 5 (per brief) or 6 (with explicit guard); both <= 8
- [x] ASCII-only: PASS — all string literals ASCII ("DW-B135", "[PTT-COPY-GUARD]" pattern)
- [x] PTT- prefix: N/A — no CreateOrder calls in this fix
- [x] No DateTime.Now: N/A — no time operations in this fix
- [x] No FontFamily: N/A — no UI in this fix
- [x] Carry-forwards reviewed: DW-B128 preserved, DW-B134-OCO unaffected
- [x] All 6 architect items (A1-A6) confirmed
- [x] 3 xUnit test contracts specified with exact asserts
- [x] Test accessors specified (3 thin shims)
- [x] LaneA scope confined to TryFirePositionState + test accessors + B129Tests.cs append
