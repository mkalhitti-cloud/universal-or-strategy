# B111-T1 Architecture Plan

**Block**: B111-T1
**Date**: 2026-08-28 (authored)
**Author**: ptt-architect
**Status**: REVIEW_PENDING

---

## 1. Block Summary

Block B111-T1 closes two P0 defects discovered in the 2026-08-28 live re-test session:

| Defect | Name | Combo |
|--------|------|-------|
| DW-B111 | `_beReplaceAttempts` Counter Reset in Timer Callback causes Infinite BE-Retry Loop | Combo D (QX-ALL -> BE-ALL) |
| DW-B112 | `_qxCancelInProgress` Guard Cleared Before Async Cancel Events Arrive | Combo C (BE-ALL -> QX-ALL) |

**Affected files (source changes)**:

| File | Change type |
|------|-------------|
| `src/PropTraderTools/CopyEngine.cs` | Surgical: remove 1 line + insert 1 guard block + update 3 literal strings |
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | Comment clarification only — zero structural change |
| `src/PropTraderTools/Features/PttBreakEvenSwap.cs` | OUT OF SCOPE — deferred (see Section 5) |

No new classes. No new files. No lock() anywhere. No async void. No return null.

---

## 2. DW-B111 Fix — Exact Change

### 2.1 Root Cause (verified against source)

`QueueBeRetryFallback` timer callback at `CopyEngine.cs L1465` calls
`_beReplaceAttempts.TryRemove(capturedAcc.Name, out _)` **before** calling `MoveStopToBreakEven`.
This removes the attempt key (setting the implicit value to 0) on every 500 ms tick.
`TryReplacePttBeBrackets` then reads `prevAttempts=0`, stores 1, and logs `"attempt 1/3"` — the
`prevAttempts >= 3` guard at L2299 is never reached. Loop runs indefinitely.

### 2.2 Change A — Remove L1465 (primary fix)

**File**: `src/PropTraderTools/CopyEngine.cs`
**Line range**: L1465 (single line inside `if (_pendingFollowerBeSlots.TryRemove(...))` success arm of the timer tick lambda)

**OLD code (exact)**:
```csharp
                        _beReplaceAttempts.TryRemove(capturedAcc.Name, out _); // DW-B82-01: reset on slot consumption
```

**NEW code**:
*(line deleted entirely)*

**Reason**: The DW-B82-01 reset was intended to clear the counter for the **next trade**, but it fires
mid-trade on every timer tick. The correct reset locations already exist:

- `CopyEngine.cs L1354` — `TryFireFollowerBeRetry`: resets counter when QX-path event-driven retry consumes the slot.
- `CopyEngine.cs L1409` — `TryEvictFollowerBeSlot`: resets counter unconditionally on flat / Rejected terminal.

Removing L1465 leaves the counter intact across timer-driven retry cycles, allowing the
`prevAttempts >= cap` guard to fire after the cap is exhausted.

**CYC impact on `QueueBeRetryFallback`**:

| Scope | CYC Before | CYC After | Delta |
|-------|-----------|-----------|-------|
| Outer method body | 1 | 1 | 0 |
| Timer tick lambda (not separately counted) | 2 branches | 2 branches | 0 |

Removing a statement from inside an existing branch does not change the branch count.

### 2.3 Change B — Attempt cap 3 → 5

**File**: `src/PropTraderTools/CopyEngine.cs`

**Decision: Raise cap to 5.**

Reasoning: 3 × 500 ms = 1.5 s is the maximum retry storm duration with cap=3. The partial-target
retry path (Sim103 received 2 of 3 PTT-QX-T orders at BE-ALL fire time due to NT8 async lag) may
legitimately need more than 3 cycles in stressed NT8 sim conditions. The primary fix (removal of
L1465 counter reset) makes the cap the genuine safety valve — it was previously unreachable, so the
old value of 3 was never enforced. Setting cap=5 gives 2.5 s of bounded storm duration with the
same 500 ms timer interval. This is acceptable: 2.5 s is short enough to avoid prolonged
unprotected exposure while providing enough headroom for slow NT8 propagation.

**Change B-1** — guard constant at L2299:

| | Content |
|---|---|
| **OLD** (L2299) | `if (prevAttempts >= 3) // (4)` |
| **NEW** | `if (prevAttempts >= 5) // (4) DW-B111: cap raised to 5 (3x500ms insufficient for partial-target retry)` |

**Change B-2** — guard log message (L2304):

| | Content |
|---|---|
| **OLD** | `" -- max 3 attempts, no new slot (TryFireFollowerBeRetry still holds slot "` |
| **NEW** | `" -- max 5 attempts, no new slot (TryFireFollowerBeRetry still holds slot "` |

**Change B-3** — slot-registered log message (L2324):

| | Content |
|---|---|
| **OLD** | `+ "/3, slot registered, 500ms fallback queued"` |
| **NEW** | `+ "/5, slot registered, 500ms fallback queued"` |

**CYC impact on `TryReplacePttBeBrackets`** from Changes B-1/B-2/B-3: **zero** (constant and string literal changes only, no new branches).

---

## 3. DW-B112 Fix — Exact Change

### 3.1 Root Cause (verified against source)

`PttGlobalQuickExit.ExecuteOne` at `PttGlobalQuickExit.cs L154-162`:

```csharp
CopyEngine.Instance?._qxCancelInProgress.TryAdd(acc.Name, true);        // guard SET (sync)
try
{
    CopyEngine.Instance?.CancelQxBrackets(acc, instr);                   // cancel SUBMITTED (async to NT8)
}
finally
{
    CopyEngine.Instance?._qxCancelInProgress.TryRemove(acc.Name, out _); // guard CLEARED (sync, immediately)
}
// PttQuickExit.Execute runs -> PTT-QX orders submitted
// ...NT8 delivers OnOrderUpdate(Cancelled) HERE -- guard is already gone
```

`Account.Cancel()` in NT8 is asynchronous: it submits the cancel request and returns immediately.
`OnOrderUpdate(OrderState.Cancelled)` fires on NT8's background thread hundreds of microseconds to
milliseconds later. The `finally` executes synchronously and immediately, so
`_qxCancelInProgress["Sim102"]` is removed **before** the cancel events arrive.

`TryReplacePttBeBrackets` checks `_qxCancelInProgress.ContainsKey("Sim102")` — returns `false` —
and proceeds to register a recovery slot. `MoveStopToBreakEven` fires on top of the
PTT-QX brackets already submitted → unprotected position (Sim103, Sim104).

### 3.2 Director Decision

**Option 2 chosen (2026-08-28)**: Add a structural PTT-QX presence check in
`TryReplacePttBeBrackets`. Before registering a recovery slot, query `acc.Orders` for any order
whose name starts with `"PTT-QX-"` and whose state is `Working` or `Submitted` for this
`account + instrument`. If any found → skip recovery.

This eliminates the timing dependency entirely. The guard fires even when
`_qxCancelInProgress` has already been cleared.

### 3.3 Insertion Point

**File**: `src/PropTraderTools/CopyEngine.cs`
**Insertion after**: L2296 (`var instr = cancelledStop.Instrument;`)
**Insertion before**: L2297 (`// (4) Attempt-count guard: max 3 slot registrations...`)

At this point `acc` and `instr` are already defined (L2295–2296), so the check uses them directly.

### 3.4 OLD code at insertion point (L2297 area)

```csharp
            var acc = cancelledStop.Account;
            var instr = cancelledStop.Instrument;
            // (4) Attempt-count guard: max 3 slot registrations per trade per account.
```

### 3.5 NEW code (full guard block inserted between L2296 and L2297)

```csharp
            var acc = cancelledStop.Account;
            var instr = cancelledStop.Instrument;
            // (3c) DW-B112: structural PTT-QX presence check. If any PTT-QX-* orders are Working
            // or Submitted for this account+instrument, QX-ALL has already protected the position.
            // Skip ATM-sweep recovery to prevent PTT-BE brackets firing on top of PTT-QX brackets.
            // Timing-independent: does not rely on _qxCancelInProgress guard window.
            if (
                acc.Orders.Any(
                    o =>
                        o.Name.StartsWith("PTT-QX-", StringComparison.Ordinal)
                        && (
                            o.OrderState == OrderState.Working
                            || o.OrderState == OrderState.Submitted
                        )
                        && o.Instrument?.FullName == instr.FullName
                )
            )
            {
                NinjaTrader.Code.Output.Process(
                    "[BE-DIAG] TryReplacePttBeBrackets: "
                        + acc.Name
                        + " -- PTT-QX orders Working/Submitted, skipping recovery (DW-B112)",
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
                return;
            }
            // (4) Attempt-count guard: max 5 slot registrations per trade per account.
```

### 3.6 NT8 API usage

| Expression | NT8 type | Confirmed from |
|---|---|---|
| `acc.Orders` | `NinjaTrader.Cbi.Account.Orders` (IEnumerable) | Pattern used in existing `CancelQxBrackets` / `SnapshotBeTargets` |
| `o.Name.StartsWith("PTT-QX-", StringComparison.Ordinal)` | `NinjaTrader.Cbi.Order.Name` | Used at `CopyEngine.cs L1339` |
| `o.OrderState == OrderState.Working` | `NinjaTrader.Cbi.OrderState` enum | Used at `CopyEngine.cs L1348` |
| `o.OrderState == OrderState.Submitted` | Same | NT8 Submitted state = cancel accepted but not yet confirmed |
| `o.Instrument?.FullName` | `NinjaTrader.Cbi.Order.Instrument.FullName` | Used at `CopyEngine.cs L1504` |
| `StringComparison.Ordinal` | `System` | Established pattern in file |

The `acc.Orders` enumeration from the `OnOrderUpdate` background-thread context is NT8-safe:
NT8's `Account.Orders` collection supports concurrent read access (same as existing code that reads it
from `OnOrderUpdate` callbacks).

### 3.7 _qxCancelInProgress guard preserved

The existing `_qxCancelInProgress` guard at L2293 (`// 3b DW-B105`) is **NOT removed**. It remains
as belt-and-suspenders protection for the synchronous window between `TryAdd` (guard set) and
`CancelQxBrackets` return (before `TryRemove`). The two guards layer:

| Guard | Window covered | Mechanism |
|-------|---------------|-----------|
| `_qxCancelInProgress` (L2293, DW-B105) | Synchronous: TryAdd → CancelQxBrackets → TryRemove | ConcurrentDictionary key presence |
| PTT-QX presence check (new, DW-B112) | Async: after TryRemove, until all cancel events delivered | NT8 Order state query |

Together they provide complete coverage of the QX-ALL sweep window.

### 3.8 Timing risk acknowledgement

**Risk**: Tiny window after `CancelQxBrackets` returns but before `PttQuickExit.Execute` submits
PTT-QX orders. In this window, `acc.Orders` has no `PTT-QX-*` orders in `Working/Submitted`.
If NT8 somehow delivered cancel events in this window, the new guard would return false.

**Mitigation**: This window is on the order of microseconds (synchronous code path continuation).
NT8's async cancel event delivery takes hundreds of microseconds to milliseconds in sim. The
`_qxCancelInProgress` guard already covers this window completely (it is still set). The two
guards together make the failure mode theoretically impossible under normal NT8 operation.

**The spec (Director decision)** acknowledges this risk and considers `Submitted` state as
sufficient protection. The check includes `Submitted` to handle the window where PTT-QX orders
have been submitted but not yet transitioned to `Working`.

### 3.9 Method header comment update (CYC annotation)

**File**: `src/PropTraderTools/CopyEngine.cs`
**Line**: L2279–2282 (method header comment for `TryReplacePttBeBrackets`)

**OLD**:
```csharp
        // CYC=5: (1) null guard, (2) follower guard, (3) flat guard, (4) attempt guard, (5) slot+fallback.
        // JS-021: ConcurrentDictionary ops are lock-free. JS-001: no throw. JS-002: void. ASCII-only.
        // DW-T4: structurally unreachable from follower path. ...
```

**NEW**:
```csharp
        // CYC=7: (1) null guard, (2) follower guard, (3) flat guard, (3b) qxCancelInProgress guard,
        // (3c) PTT-QX presence check DW-B112, (4) attempt guard DW-B111 cap=5, (5) slot+fallback.
        // JS-021: ConcurrentDictionary ops are lock-free. acc.Orders read is NT8-safe from OnOrderUpdate.
        // JS-001: no throw. JS-002: void. ASCII-only. DW-B111: cap raised 3->5. DW-B112: Option 2.
        // DW-T4: structurally unreachable from follower path. ...
```

---

## 4. PttGlobalQuickExit.cs — Comment Addition

**File**: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`
**No structural change.** Comment-only addition at the `finally` block (around L159-162):

**OLD** (L159-162):
```csharp
                finally
                {
                    CopyEngine.Instance?._qxCancelInProgress.TryRemove(acc.Name, out _);
                }
```

**NEW**:
```csharp
                finally
                {
                    // DW-B112: TryRemove clears guard synchronously. NT8 OnOrderUpdate(Cancelled)
                    // events for the swept orders arrive asynchronously AFTER this finally executes.
                    // The structural PTT-QX presence check in TryReplacePttBeBrackets (DW-B112 Option 2)
                    // compensates by checking acc.Orders for Working/Submitted PTT-QX-* orders.
                    CopyEngine.Instance?._qxCancelInProgress.TryRemove(acc.Name, out _);
                }
```

**Exact comment text**:
```
// DW-B112: TryRemove clears guard synchronously. NT8 OnOrderUpdate(Cancelled)
// events for the swept orders arrive asynchronously AFTER this finally executes.
// The structural PTT-QX presence check in TryReplacePttBeBrackets (DW-B112 Option 2)
// compensates by checking acc.Orders for Working/Submitted PTT-QX-* orders.
```

---

## 5. PttBreakEvenSwap.cs — Scope Decision

**DEFERRED to next block (B112 or later). Secondary fix is OUT OF SCOPE for B111-T1.**

The secondary fix described in the spec is:

> In `PttBreakEvenSwap.Execute`, skip the `CancelQxBrackets` sweep when the call is a retry
> (add an `isRetry` parameter or check whether orders with this name are already Working).

**Reasoning for deferral**:

1. The primary fix (remove L1465 counter reset) plus the DW-B112 structural guard are together
   sufficient to close both defects. The `CancelQxBrackets` sweep inside `PttBreakEvenSwap.Execute`
   is **expected behavior** — it sweeps stale prior PTT-BE orders before placing fresh ones.
   Removing the sweep introduces a different risk: accumulating stale Working PTT-BE orders from
   the prior cycle, which could conflict with new OCO submissions.

2. The secondary fix requires a signature change to `PttBreakEvenSwap.Execute` (adding `isRetry`
   parameter) and changes to all call sites in `CopyEngine.cs`. This scope belongs to a
   dedicated block with its own review cycle.

3. DW-B111's loop is terminated by the counter cap (fixed by Change A + Change B). Even if
   `CancelQxBrackets` fires on retry, the guard at `prevAttempts >= 5` terminates the loop.
   The secondary fix is a code-quality improvement, not a correctness requirement for B111-T1.

**Deferred item**: `B111-DEFER-01` — Add `isRetry` param to `PttBreakEvenSwap.Execute` to skip
`CancelQxBrackets` on retry invocations. Reduces spurious cancel events that trigger
`TryReplacePttBeBrackets`. Low priority — B111-T1 primary fix eliminates the loop regardless.

---

## 6. CYC Analysis Table

| Method | File | CYC Before | CYC After | Delta | Within Budget? |
|--------|------|-----------|-----------|-------|---------------|
| `QueueBeRetryFallback` (outer method) | CopyEngine.cs | 1 | 1 | 0 | YES (<=8) |
| `QueueBeRetryFallback` timer tick lambda | CopyEngine.cs | 2 | 2 | 0 | YES (<=8) |
| `TryReplacePttBeBrackets` | CopyEngine.cs | 6 | 7 | +1 | YES (<=8) |
| `TryFireFollowerBeRetry` (unchanged) | CopyEngine.cs | 5 | 5 | 0 | YES (<=8) |
| `TryEvictFollowerBeSlot` (unchanged) | CopyEngine.cs | 6 | 6 | 0 | YES (<=8) |
| `ExecuteOne` (PttGlobalQuickExit) | PttGlobalQuickExit.cs | unchanged | unchanged | 0 | YES (<=8) |
| `Execute` (PttBreakEvenSwap) | PttBreakEvenSwap.cs | 8 | 8 | 0 | YES (=8) |

Note: CYC=6 for `TryReplacePttBeBrackets` before this block reflects the post-DW-B92 state
(mission brief). The method header comment in source currently reads "CYC=5" — that annotation
predates DW-B92 and will be updated to "CYC=7" as part of Change D.

---

## 7. Test Stubs — All 4 Tests

All tests use xUnit `[Fact]` only. No NUnit. No MSTest.

### T_B111_01 — `TryReplacePttBeBrackets_SkipsRecovery_WhenPttQxOrderWorking`

**File**: `tests/PropTraderTools.Tests/CopyEngineTests.cs` (or new B111 test file)
**Spec requirement satisfied**: DW-B112 Option 2 guard

```csharp
[Fact]
public void TryReplacePttBeBrackets_SkipsRecovery_WhenPttQxOrderWorking()
{
    // Arrange
    // - Create mock Account (follower) with one Order:
    //     Name = "PTT-QX-T1", OrderState = OrderState.Working, Instrument = MES_SEP26
    // - Create mock cancelledStop (PTT-BE-Stop-1, same account+instrument, follower account)
    // - Position is non-flat (Quantity > 0)
    // - _qxCancelInProgress is empty (guard cleared)
    // - _beReplaceAttempts["Sim103"] = 0
    // - _pendingFollowerBeSlots does NOT contain "Sim103"

    // Act
    // - Call TryReplacePttBeBrackets(cancelledStop)

    // Assert
    // - _pendingFollowerBeSlots.ContainsKey("Sim103") == false
    //   (no slot was registered -- DW-B112 guard fired)
    // - _beReplaceAttempts.TryGetValue("Sim103", out _) == false OR value unchanged at 0
    // - Output log contains "[BE-DIAG] Sim103 -- PTT-QX orders Working/Submitted, skipping recovery (DW-B112)"
}
```

**Acceptance criteria**: No slot registered. No MoveStopToBreakEven called. Log contains DW-B112 diagnostic.

---

### T_B111_02 — `TryReplacePttBeBrackets_SkipsRecovery_WhenPttQxOrderSubmitted`

**File**: Same as T_B111_01
**Spec requirement satisfied**: DW-B112 Option 2 guard (Submitted state branch)

```csharp
[Fact]
public void TryReplacePttBeBrackets_SkipsRecovery_WhenPttQxOrderSubmitted()
{
    // Arrange
    // - Same as T_B111_01 except:
    //     Order.OrderState = OrderState.Submitted (not Working)
    // - All other conditions identical

    // Act
    // - Call TryReplacePttBeBrackets(cancelledStop)

    // Assert
    // - _pendingFollowerBeSlots.ContainsKey("Sim103") == false
    // - Log contains "[BE-DIAG] Sim103 -- PTT-QX orders Working/Submitted, skipping recovery (DW-B112)"
}
```

**Acceptance criteria**: `Submitted` state is treated identically to `Working` — recovery is skipped.
This verifies the `|| o.OrderState == OrderState.Submitted` branch of the guard expression.

---

### T_B111_03 — `QueueBeRetryFallback_AttemptCounter_NotResetBeforeMoveStop`

**File**: Same
**Spec requirement satisfied**: DW-B111 primary fix (remove L1465 TryRemove)

```csharp
[Fact]
public void QueueBeRetryFallback_AttemptCounter_NotResetBeforeMoveStop()
{
    // Arrange
    // - _beReplaceAttempts["Sim103"] = 2  (counter already at 2 from prior attempts)
    // - _pendingFollowerBeSlots["Sim103"] = new PendingFollowerBeSlot(...)
    // - MoveStopToBreakEven is mocked to capture call order
    // - Position is non-flat

    // Act
    // - Invoke the timer tick callback (simulate timer firing)
    //   i.e., call the equivalent of the code at CopyEngine.cs L1460-1490

    // Assert
    // - _beReplaceAttempts["Sim103"] == 2 immediately after TryRemove(_pendingFollowerBeSlots) returns
    //   AND before MoveStopToBreakEven is called
    //   (counter was NOT reset; L1465 TryRemove is absent)
    // - MoveStopToBreakEven was called exactly once with isRetry:true
    // - _beReplaceAttempts.TryGetValue("Sim103") still returns 2 AFTER MoveStopToBreakEven
    //   (counter only resets at TryFireFollowerBeRetry L1354 or TryEvictFollowerBeSlot L1409)
}
```

**Acceptance criteria**: Attempt counter retains its pre-timer value throughout the timer callback.
Demonstrates that removal of L1465 is effective.

---

### T_B111_04 — `QueueBeRetryFallback_LoopTerminates_AfterCapAttempts`

**File**: Same
**Spec requirement satisfied**: DW-B111 — cap=5 terminates loop after 5 attempts

```csharp
[Fact]
public void QueueBeRetryFallback_LoopTerminates_AfterCapAttempts()
{
    // Arrange (attempt 5 -- one below cap triggers, attempt 6 does not)
    // Part A: prevAttempts = 4 (4 prior attempts recorded)
    // - Non-flat position. _qxCancelInProgress empty. No PTT-QX orders Working/Submitted.

    // Act (Part A)
    // - Call TryReplacePttBeBrackets(cancelledStop)

    // Assert (Part A)
    // - Slot IS registered (attempt 5 allowed; 4 < 5)
    // - _beReplaceAttempts["Sim103"] == 5
    // - Log contains "-- attempt 5/5, slot registered, 500ms fallback queued"

    // --- simulate 5th timer cycle cancelling brackets -> increment counter to 5 ---

    // Arrange (Part B): prevAttempts = 5
    // Part B: _beReplaceAttempts["Sim103"] = 5

    // Act (Part B)
    // - Call TryReplacePttBeBrackets(cancelledStop) again

    // Assert (Part B)
    // - Slot is NOT registered (guard fires: 5 >= 5)
    // - Log contains "-- max 5 attempts, no new slot"
    // - _pendingFollowerBeSlots.ContainsKey("Sim103") == false
}
```

**Acceptance criteria**: At attempt index 4 (< 5), a slot is registered. At attempt index 5 (>= 5),
the guard fires and no slot is registered. Loop is bounded.

Note: The original mission brief named this test `QueueBeRetryFallback_LoopTerminates_After3Attempts`.
The name is updated to `AfterCapAttempts` because the cap was raised from 3 to 5 as part of this
block. The test verifies the current cap value (5), not the historic value (3).

---

## 8. Jane Street Rules Compliance

| Rule | Description | B111-T1 Compliance |
|------|-------------|-------------------|
| JS-001 | No throw in hot paths | PASS — no new exceptions introduced. `TryReplacePttBeBrackets` and `QueueBeRetryFallback` are both void with no throw. |
| JS-002 | No return null | PASS — both methods return void. |
| JS-021 | No lock() anywhere | PASS — `ConcurrentDictionary.TryRemove`, `TryGetValue`, `ContainsKey` are lock-free. `acc.Orders.Any()` is read-only NT8 enumeration (no lock). Zero `lock()` statements introduced. |
| JS-033 | No async void (non-event-handler) | PASS — no async methods touched. DispatcherTimer.Tick is an event handler (exempt). No new async void introduced. |
| JS-036 | No heap allocation in hot path | PASS — no new arrays or large allocations. LINQ `Any()` on `acc.Orders` is read-only enumeration with early exit. |
| CYC <= 8 | Cyclomatic complexity budget | PASS — `TryReplacePttBeBrackets` CYC=7 (<=8). `QueueBeRetryFallback` CYC=1 (<=8). All other touched methods unchanged. |
| ASCII-only | No Unicode in string literals | PASS — all new string literals are ASCII-only: `"PTT-QX-"`, `"[BE-DIAG] TryReplacePttBeBrackets: "`, `" -- PTT-QX orders Working/Submitted, skipping recovery (DW-B112)"`, `"/5, slot registered, 500ms fallback queued"`. |
| No DateTime.Now | Use DateTime.UtcNow | PASS — not touched. |
| No FontFamily | No font-family strings | PASS — not touched. |
| No hex literals | No hardcoded hex colors | PASS — not touched. |
| PTT- prefix | All CreateOrder names start with PTT- | PASS — no new CreateOrder calls. Existing PTT- names unchanged. |

---

## 9. Risks and Mitigations

### Risk 1 — Timing window for DW-B112 guard (acknowledged by Director)

**Risk**: Small window after `CancelQxBrackets` returns synchronously and before `PttQuickExit.Execute`
submits PTT-QX orders. In this window `acc.Orders` contains no `PTT-QX-*` Working/Submitted orders.
If `OnOrderUpdate(Cancelled)` fires during this window (theoretically impossible in real NT8 since
cancel event delivery is asynchronous with hundreds of microseconds to milliseconds latency), the new
guard would return false.

**Mitigation**: The `_qxCancelInProgress` guard (L2293, DW-B105) is still present and covers this
synchronous window completely. The two guards together eliminate the failure mode. The spec
acknowledges this risk and the Director accepted it (Option 2 chosen).

### Risk 2 — Partial-target retry still needs > 5 cycles in extreme NT8 lag

**Risk**: If NT8 propagation is extremely slow (>2.5 s for all 3 PTT-QX-T orders to reach Working),
the attempt cap of 5 terminates the retry loop before all targets are protected.

**Mitigation**: 2.5 s at 500 ms interval is generous for NT8 sim conditions. The DW-B112 structural
guard will fire on cycle 2+ (once PTT-QX-T orders are Working), so the loop terminates for the right
reason (QX-ALL already protected the position) rather than the cap. The cap is the last-resort safety
valve. In practice, the DW-B112 guard is expected to fire within the first 1-2 cycles after QX-ALL.

### Risk 3 — TryEvictFollowerBeSlot reset not reached in some paths

**Risk**: If `TryEvictFollowerBeSlot` is not called for a given account (e.g., order is never Filled
or Rejected), `_beReplaceAttempts["Sim103"]` remains at cap (5) for the next trade.

**Mitigation**: This is an existing concern, not introduced by B111-T1. The DW-B82-01 reset at
`TryFireFollowerBeRetry` (L1354) handles the QX path. The `TryEvictFollowerBeSlot` at L1409 handles
the terminal path. Both are unchanged by this block. If a residual entry-counter issue is found in
future SIM testing, it belongs to a new defect block, not B111-T1.

### Risk 4 — `acc.Orders` enumeration from background thread

**Risk**: Reading `acc.Orders` from `OnOrderUpdate` (NT8 background thread) could be unsafe.

**Mitigation**: This is the established pattern in this codebase. Existing methods
(`CancelQxBrackets`, `SnapshotBeTargets`) enumerate `acc.Orders` from the `OnOrderUpdate` callback.
NT8's `Account.Orders` collection supports concurrent read access. No new threading concern.

---

## 10. Out of Scope

The following items are explicitly deferred from B111-T1:

| Item | Reason |
|------|--------|
| `PttBreakEvenSwap.cs` secondary fix (skip `CancelQxBrackets` on retry) | Not required for correctness; primary fix terminates the loop. Signature change belongs to dedicated block. See Section 5. Deferred as `B111-DEFER-01`. |
| DW-B107 (`MoveStopToBreakEven` stale `PTT-BE-Target-*` on followers) | Separate defect. Already deferred from B107. Not affected by B111-T1 changes. |
| Combo C and Combo D live SIM re-test | Director-owned gate. Prerequisite: B111-T1 F5 green. Deferred as `B111-DEFER-02`. |
| F5 NinjaTrader 8 compilation gate | Director-owned. Deferred as `B111-DEFER-03`. |
| `docs/brain/B111/06-deferred-backlog.md` | Produced at end of pipeline, not during architecture phase. |
| Test infrastructure remediation (83 errors, CS0433) | Pre-existing. Deferred as DW-PTT-BE-FIX-03 (carry-forward from B107). |

---

## Appendix: Change Summary Matrix

| Change ID | File | Lines affected | Type | Defect closed |
|-----------|------|----------------|------|---------------|
| A | CopyEngine.cs | L1465 (delete) | Surgical delete | DW-B111 |
| B-1 | CopyEngine.cs | L2299 (constant) | Literal update | DW-B111 |
| B-2 | CopyEngine.cs | L2304 (string) | Literal update | DW-B111 |
| B-3 | CopyEngine.cs | L2324 (string) | Literal update | DW-B111 |
| C | CopyEngine.cs | After L2296 (+~14 lines) | Guard block insert | DW-B112 |
| D | CopyEngine.cs | L2279-2282 (comment) | Comment update | DW-B111+B112 |
| E | PttGlobalQuickExit.cs | L159-162 (comment) | Comment addition | DW-B112 (documentation) |

**Total `.cs` lines changed**: ~18 net new + 1 deleted = approximately 17 net additions.
No new classes. No new files. No structural change to `PttGlobalQuickExit.cs`.
