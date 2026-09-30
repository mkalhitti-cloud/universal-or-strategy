# B111-T1 Tickets

**Block**: B111-T1
**Date**: 2026-08-28
**Author**: ptt-architect
**Plan Status**: REVIEW_PASS (02-plan-review.md, 2026-08-28)
**Ticket Count**: 1 (T1)

---

## Ticket T1 — B111-T1: Fix DW-B111 (Infinite BE-Retry Loop) + DW-B112 (QX Presence Guard)

---

### Spec Requirements

| Defect ID | Description | Plan Section |
|-----------|-------------|-------------|
| DW-B111 | `_beReplaceAttempts` Counter Reset in Timer Callback causes Infinite BE-Retry Loop (Combo D) | Sections 2.2 + 2.3 |
| DW-B112 | `_qxCancelInProgress` Guard Cleared Before Async Cancel Events Arrive (Combo C) | Section 3 |

**Spec coverage per plan review Section 5 (all requirements satisfied)**:
- DW-B111: Remove counter reset from timer callback (Change A)
- DW-B111: Raise attempt cap 3 → 5 with reasoning (Changes B-1/B-2/B-3)
- DW-B112: Structural PTT-QX presence check Option 2 (Change C)
- DW-B112: Update PttGlobalQuickExit.cs comment (Change E)
- DW-B112: Preserve belt-and-suspenders `_qxCancelInProgress` guard at L2293
- Method header comment update to CYC=7 (Change D)

---

### Files Changed

| File | Role |
|------|------|
| `src/PropTraderTools/CopyEngine.cs` | Primary edit — 4 changes (A, B-1/B-2/B-3, C, D) |
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | Comment addition only — zero structural change (Change E) |
| `src/PropTraderTools/Tests/B111Tests.cs` | New test file — 4 xUnit [Fact] tests |

---

### 7-Scan Checklist (MANDATORY — defense in depth, all 7 items required)

- [ ] Scan 1: `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` — must return zero new matches in changed lines
- [ ] Scan 2: `grep -n "async void" src/PropTraderTools/CopyEngine.cs` — must return zero new matches in changed lines
- [ ] Scan 3: `grep -n "return null" src/PropTraderTools/CopyEngine.cs` — must return zero new matches in changed lines
- [ ] Scan 4: `grep -rn "lock(" src/PropTraderTools/Features/PttGlobalQuickExit.cs` — must return zero results
- [ ] Scan 5: `grep -rn "async void" src/PropTraderTools/Features/PttGlobalQuickExit.cs` — must return zero results
- [ ] Scan 6: `python scripts/complexity_audit.py` — `TryReplacePttBeBrackets` must report CYC <= 8 (expected 7); `QueueBeRetryFallback` must report CYC <= 8 (expected 1)
- [ ] Scan 7: ASCII-only check on changed files — `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs src/PropTraderTools/Features/PttGlobalQuickExit.cs src/PropTraderTools/Tests/B111Tests.cs` — must return zero results

**All 7 scans must pass before the engineer marks T1 complete.**

---

### Change A — DW-B111: Remove TryRemove from timer callback

**File**: `src/PropTraderTools/CopyEngine.cs`
**Line range**: L1465 (single line, inside the `if (_pendingFollowerBeSlots.TryRemove(...))` success arm of the timer tick lambda inside `QueueBeRetryFallback`)

**OLD CODE** (exact line to delete):
```csharp
                        _beReplaceAttempts.TryRemove(capturedAcc.Name, out _); // DW-B82-01: reset on slot consumption
```

**NEW CODE**: *(line deleted entirely — no replacement)*

**Why**: This `TryRemove` was the root cause of DW-B111. It reset the counter on every 500 ms timer tick (before `MoveStopToBreakEven` was called), making the `prevAttempts >= cap` guard at L2299 permanently unreachable. Removing this line allows the counter to accumulate across timer cycles so the cap guard can fire.

**Correct reset locations** (already present, unchanged by this ticket):
- L1354 (`TryFireFollowerBeRetry`): resets counter when QX-path event-driven retry consumes the slot.
- L1409 (`TryEvictFollowerBeSlot`): resets counter unconditionally on flat / Rejected terminal.

**CYC before**: `QueueBeRetryFallback` outer method = 1; timer tick lambda = 2 branches
**CYC after**: `QueueBeRetryFallback` outer method = 1; timer tick lambda = 2 branches *(delta = 0; removing a statement from inside an existing branch does not change branch count)*

---

### Change B — DW-B111: Attempt cap raise 3 → 5

Three literal-only changes in `src/PropTraderTools/CopyEngine.cs`. No new branches — CYC delta = 0 for all three.

#### Change B-1 — Guard constant (L2299)

**File**: `src/PropTraderTools/CopyEngine.cs`
**Line**: L2299

**OLD CODE**:
```csharp
            if (prevAttempts >= 3) // (4)
```

**NEW CODE**:
```csharp
            if (prevAttempts >= 5) // (4) DW-B111: cap raised to 5 (3x500ms insufficient for partial-target retry)
```

**Reasoning** (from plan Section 2.3): 3 × 500 ms = 1.5 s was previously unreachable (L1465 reset made it so). Cap=5 = 2.5 s of bounded storm duration. Provides sufficient headroom for slow NT8 sim propagation while remaining short enough to avoid prolonged unprotected exposure.

#### Change B-2 — Guard log message (L2304)

**File**: `src/PropTraderTools/CopyEngine.cs`
**Line**: L2304

**OLD CODE**:
```csharp
                    " -- max 3 attempts, no new slot (TryFireFollowerBeRetry still holds slot "
```

**NEW CODE**:
```csharp
                    " -- max 5 attempts, no new slot (TryFireFollowerBeRetry still holds slot "
```

#### Change B-3 — Slot-registered log message (L2324)

**File**: `src/PropTraderTools/CopyEngine.cs`
**Line**: L2324

**OLD CODE**:
```csharp
                    + "/3, slot registered, 500ms fallback queued"
```

**NEW CODE**:
```csharp
                    + "/5, slot registered, 500ms fallback queued"
```

**CYC before/after Changes B-1/B-2/B-3**: Zero CYC delta (constant and string literal changes only).

---

### Change C — DW-B112: PTT-QX presence check in TryReplacePttBeBrackets

**File**: `src/PropTraderTools/CopyEngine.cs`
**Insertion point**: After L2296 (`var instr = cancelledStop.Instrument;`), before L2297 (`// (4) Attempt-count guard: max 3 slot registrations...`)

At this insertion point `acc` (L2295) and `instr` (L2296) are already in scope.

**W1 Resolution**: The engineer MUST use `.ToList().Any(...)` (not bare `.Any()`) for consistency with the codebase's documented safety pattern at L2414 (`// acc.Orders.ToList() snapshot prevents InvalidOperationException`). This is the safer form and matches the majority of `acc.Orders` iteration sites in this file (L2417, L2818, L2936, L2967, L3649). The `.ToList()` snapshot eliminates any risk of `InvalidOperationException` if the NT8 orders collection is mutated during enumeration, and is preferred over the bare `.Any()` used at L757 (`foreach (Order o in acc.Orders)`). Neither form changes CYC. **W1 is resolved by adopting option (b): `.ToList().Any(...)`.**

**NEW CODE** (full guard block — complete, compilable C#, inserted between L2296 and the original L2297):

```csharp
            var acc = cancelledStop.Account;
            var instr = cancelledStop.Instrument;
            // (3c) DW-B112: structural PTT-QX presence check. If any PTT-QX-* orders are Working
            // or Submitted for this account+instrument, QX-ALL has already protected the position.
            // Skip ATM-sweep recovery to prevent PTT-BE brackets firing on top of PTT-QX brackets.
            // Timing-independent: does not rely on _qxCancelInProgress guard window.
            // W1 resolved: .ToList() snapshot used for consistency with L2414 safety pattern.
            if (
                acc.Orders
                    .ToList()
                    .Any(
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

**Note**: The line `var acc = cancelledStop.Account;` and `var instr = cancelledStop.Instrument;` shown above are the existing L2295–L2296 lines for context — they are NOT re-added. Only the guard block between them and the original `// (4)` comment is new code.

**`_qxCancelInProgress` guard**: Confirmed preserved at L2293 (`if (_qxCancelInProgress.ContainsKey(cancelledStop.Account.Name)) return;`). This guard is NOT removed. It provides belt-and-suspenders coverage of the synchronous window (TryAdd → CancelQxBrackets → TryRemove). The new PTT-QX presence check (Change C) provides the complementary async-window coverage (after TryRemove, until all cancel events are delivered).

**CYC before** (TryReplacePttBeBrackets): 6
**CYC after** (TryReplacePttBeBrackets): 7
**Delta**: +1 (one new `if` branch)
**Budget**: Within <= 8 ✓

**NT8 API usage** (all confirmed from existing codebase patterns):

| Expression | NT8 type | Existing usage |
|---|---|---|
| `acc.Orders.ToList()` | `NinjaTrader.Cbi.Account.Orders` (IEnumerable) | `.ToList()` safety pattern at CopyEngine.cs L2414 |
| `.Any(o => ...)` | LINQ on snapshot | Established pattern post-.ToList() |
| `o.Name.StartsWith("PTT-QX-", StringComparison.Ordinal)` | `NinjaTrader.Cbi.Order.Name` | CopyEngine.cs L1338-1339 uses sub-variant `"PTT-QX-T"` |
| `o.OrderState == OrderState.Working` | `NinjaTrader.Cbi.OrderState` enum | CopyEngine.cs L1348 |
| `o.OrderState == OrderState.Submitted` | Same enum | NT8 Submitted = cancel accepted but not confirmed |
| `o.Instrument?.FullName == instr.FullName` | `NinjaTrader.Cbi.Order.Instrument.FullName` | CopyEngine.cs L1504 (non-null form used inside FindMatchingRule) |
| `StringComparison.Ordinal` | `System` | Established pattern throughout CopyEngine.cs |

---

### Change D — DW-B111 + DW-B112: Update TryReplacePttBeBrackets method header comment

**File**: `src/PropTraderTools/CopyEngine.cs`
**Lines**: L2279–L2282 (method header comment block for `TryReplacePttBeBrackets`)

**OLD CODE** (exact — stale CYC=5 annotation predating DW-B92):
```csharp
        // CYC=5: (1) null guard, (2) follower guard, (3) flat guard, (4) attempt guard, (5) slot+fallback.
        // JS-021: ConcurrentDictionary ops are lock-free. JS-001: no throw. JS-002: void. ASCII-only.
        // DW-T4: structurally unreachable from follower path. ...
```

**NEW CODE**:
```csharp
        // CYC=7: (1) null guard, (2) follower guard, (3) flat guard, (3b) qxCancelInProgress guard,
        // (3c) PTT-QX presence check DW-B112, (4) attempt guard DW-B111 cap=5, (5) slot+fallback.
        // JS-021: ConcurrentDictionary ops are lock-free. acc.Orders read is NT8-safe from OnOrderUpdate.
        // JS-001: no throw. JS-002: void. ASCII-only. DW-B111: cap raised 3->5. DW-B112: Option 2.
        // DW-T4: structurally unreachable from follower path. ...
```

*(Remaining lines of the comment block after `DW-T4: ...` are preserved unchanged.)*

---

### Change E — DW-B112: PttGlobalQuickExit.cs comment addition

**File**: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`
**Lines**: L159–L162 (`finally` block)
**Type**: Comment addition only — zero structural change.

**OLD CODE** (L159–L162):
```csharp
                finally
                {
                    CopyEngine.Instance?._qxCancelInProgress.TryRemove(acc.Name, out _);
                }
```

**NEW CODE**:
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

**Exact comment text added** (4 lines, all ASCII, no structural change):
```
// DW-B112: TryRemove clears guard synchronously. NT8 OnOrderUpdate(Cancelled)
// events for the swept orders arrive asynchronously AFTER this finally executes.
// The structural PTT-QX presence check in TryReplacePttBeBrackets (DW-B112 Option 2)
// compensates by checking acc.Orders for Working/Submitted PTT-QX-* orders.
```

---

### Tests — New File: `src/PropTraderTools/Tests/B111Tests.cs`

All tests use xUnit `[Fact]` only. No NUnit. No MSTest.
**File path**: `src/PropTraderTools/Tests/B111Tests.cs`

---

#### T_B111_01 — `TryReplacePttBeBrackets_SkipsRecovery_WhenPttQxOrderWorking`

**Full xUnit method signature**:
```csharp
[Fact]
public void TryReplacePttBeBrackets_SkipsRecovery_WhenPttQxOrderWorking()
```

**Arrange / Act / Assert**:
```csharp
// Arrange
// - mockAcc: follower account "Sim103"
//   mockAcc.Orders returns a List<Order> containing one Order:
//     o.Name = "PTT-QX-T1"
//     o.OrderState = OrderState.Working
//     o.Instrument.FullName = "MES 09-26"
// - cancelledStop: mock Order with
//     cancelledStop.Account = mockAcc
//     cancelledStop.Instrument.FullName = "MES 09-26"
//     cancelledStop.Name starts with "PTT-BE-Stop"
// - IsFollowerAccount(mockAcc) returns true
// - IsFlat(FindPosition(mockAcc, instrument)) returns false (non-flat position)
// - _qxCancelInProgress is empty (ContainsKey("Sim103") == false)
// - _beReplaceAttempts["Sim103"] = 0
// - _pendingFollowerBeSlots does NOT contain key "Sim103"
// - Output.Process is captured (mock or redirect OutputTab1)

// Act
engine.TryReplacePttBeBrackets(cancelledStop);

// Assert
Assert.False(_pendingFollowerBeSlots.ContainsKey("Sim103"));
// No slot was registered — DW-B112 guard fired before attempt-count guard
Assert.True(capturedOutput.Contains(
    "[BE-DIAG] TryReplacePttBeBrackets: Sim103 -- PTT-QX orders Working/Submitted, skipping recovery (DW-B112)"));
// Attempt counter was not incremented
Assert.False(_beReplaceAttempts.TryGetValue("Sim103", out int val) && val > 0);
```

**Regression contract**: If the DW-B112 guard is absent (i.e., bug is present), `_pendingFollowerBeSlots` would contain `"Sim103"` (a recovery slot was registered). The `Assert.False` fails, catching the regression.

---

#### T_B111_02 — `TryReplacePttBeBrackets_SkipsRecovery_WhenPttQxOrderSubmitted`

**Full xUnit method signature**:
```csharp
[Fact]
public void TryReplacePttBeBrackets_SkipsRecovery_WhenPttQxOrderSubmitted()
```

**Arrange / Act / Assert**:
```csharp
// Arrange
// - Same as T_B111_01 except:
//     o.OrderState = OrderState.Submitted   (not Working)
// - All other conditions identical to T_B111_01

// Act
engine.TryReplacePttBeBrackets(cancelledStop);

// Assert
Assert.False(_pendingFollowerBeSlots.ContainsKey("Sim103"));
Assert.True(capturedOutput.Contains(
    "[BE-DIAG] TryReplacePttBeBrackets: Sim103 -- PTT-QX orders Working/Submitted, skipping recovery (DW-B112)"));
```

**Regression contract**: If the `|| o.OrderState == OrderState.Submitted` branch is missing from the guard expression, the guard would not fire and `_pendingFollowerBeSlots` would contain `"Sim103"`. The `Assert.False` fails, catching the regression.

---

#### T_B111_03 — `QueueBeRetryFallback_AttemptCounter_NotResetBeforeMoveStop`

**Full xUnit method signature**:
```csharp
[Fact]
public void QueueBeRetryFallback_AttemptCounter_NotResetBeforeMoveStop()
```

**Arrange / Act / Assert**:
```csharp
// Arrange
// - _beReplaceAttempts["Sim103"] = 2  (counter at 2 from prior attempts)
// - _pendingFollowerBeSlots["Sim103"] = new PendingFollowerBeSlot(mockAcc, mockInstr, ...)
// - moveStopCallCount = 0 (capture call count for MoveStopToBreakEven mock)
// - capturedCounterAtMoveStop is captured: the value of _beReplaceAttempts["Sim103"]
//   immediately when MoveStopToBreakEven is called

// Act
// Invoke the timer tick callback — i.e., simulate what fires when the
// DispatcherTimer.Tick fires on the slot for "Sim103".
// This is the code path in QueueBeRetryFallback at CopyEngine.cs L1460-1490.
// (Implementation: expose a testable InvokeTimerTick(accountName) helper,
//  or access the registered timer callback via the engine's internal state.)
engine.SimulateTimerTick("Sim103");

// Assert
// Counter must NOT have been reset to 0 during the tick (L1465 TryRemove is absent)
Assert.Equal(2, capturedCounterAtMoveStop);
// MoveStopToBreakEven was called exactly once
Assert.Equal(1, moveStopCallCount);
// Counter is still 2 after MoveStopToBreakEven returned
_beReplaceAttempts.TryGetValue("Sim103", out int counterAfter);
Assert.Equal(2, counterAfter);
```

**Regression contract**: If L1465 `TryRemove` is still present (bug not fixed), `capturedCounterAtMoveStop` would be 0 (reset before `MoveStopToBreakEven`), and `counterAfter` would also be 0. Both `Assert.Equal(2, ...)` calls fail, catching the regression.

---

#### T_B111_04 — `QueueBeRetryFallback_LoopTerminates_AfterCapAttempts`

**Full xUnit method signature**:
```csharp
[Fact]
public void QueueBeRetryFallback_LoopTerminates_AfterCapAttempts()
```

**Arrange / Act / Assert**:
```csharp
// ---- Part A: Attempt 5 (index 4 prior attempts) is ALLOWED ----

// Arrange (Part A)
// - _beReplaceAttempts["Sim103"] = 4  (4 prior attempts recorded)
// - Non-flat position for Sim103
// - _qxCancelInProgress is empty
// - mockAcc.Orders returns empty list (no PTT-QX-* Working/Submitted orders)
// - _pendingFollowerBeSlots does NOT contain "Sim103"

// Act (Part A)
engine.TryReplacePttBeBrackets(cancelledStop);

// Assert (Part A): 5th attempt is within cap (4 < 5) — slot MUST be registered
Assert.True(_pendingFollowerBeSlots.ContainsKey("Sim103"));
_beReplaceAttempts.TryGetValue("Sim103", out int countAfterPartA);
Assert.Equal(5, countAfterPartA);
Assert.True(capturedOutput.Contains("attempt 5/5, slot registered, 500ms fallback queued"));

// ---- Part B: Attempt 6 (index 5 prior attempts) is BLOCKED by cap guard ----

// Arrange (Part B): simulate the 5th timer cycle having fired and removed the slot
_pendingFollowerBeSlots.TryRemove("Sim103", out _);
// counter is now 5 (the 5th attempt was recorded in Part A)
// _beReplaceAttempts["Sim103"] already == 5 from Part A

// Act (Part B)
engine.TryReplacePttBeBrackets(cancelledStop);

// Assert (Part B): 6th attempt exceeds cap (5 >= 5) — guard MUST fire, no slot registered
Assert.False(_pendingFollowerBeSlots.ContainsKey("Sim103"));
Assert.True(capturedOutput.Contains("max 5 attempts, no new slot"));
```

**Regression contract**:
- If cap is still 3 (Change B-1 not applied), Part A fails because `prevAttempts=4 >= 3` fires the guard and no slot is registered.
- If Change B-1 is applied but the cap is set to a value other than 5, Part B may not fire correctly.
- Both parts together verify the exact cap boundary (< 5 allows, >= 5 blocks).

---

### Acceptance Criteria

All criteria must be verifiable by the verifier independently from source inspection.

1. `src/PropTraderTools/CopyEngine.cs` L1465 does NOT contain `_beReplaceAttempts.TryRemove(capturedAcc.Name, out _)` or any `TryRemove` referencing `capturedAcc.Name` inside the timer tick success arm.
2. `CopyEngine.cs` L2299 reads `if (prevAttempts >= 5)` (not `>= 3`).
3. `CopyEngine.cs` L2304 log string contains `"max 5 attempts"` (not `"max 3 attempts"`).
4. `CopyEngine.cs` L2324 log string contains `"/5, slot registered"` (not `"/3, slot registered"`).
5. `CopyEngine.cs` contains a PTT-QX presence check guard block (after L2296, before the `// (4)` attempt-count comment) that calls `.ToList().Any(...)` on `acc.Orders` filtering on `StartsWith("PTT-QX-", ...)`, `OrderState.Working || OrderState.Submitted`, and `Instrument?.FullName == instr.FullName`.
6. The guard block in criterion 5 uses `.ToList()` before `.Any()` (W1 resolution — option b).
7. The guard block in criterion 5 logs `"[BE-DIAG] TryReplacePttBeBrackets: " + acc.Name + " -- PTT-QX orders Working/Submitted, skipping recovery (DW-B112)"` and `return`s.
8. `CopyEngine.cs` L2293 (`_qxCancelInProgress.ContainsKey(...)`) is preserved unchanged.
9. `CopyEngine.cs` method header comment for `TryReplacePttBeBrackets` reads `// CYC=7:` (not `CYC=5` or `CYC=6`).
10. `PttGlobalQuickExit.cs` `finally` block contains the 4-line DW-B112 comment above the `TryRemove` call.
11. `src/PropTraderTools/Tests/B111Tests.cs` exists and contains 4 `[Fact]` methods: `TryReplacePttBeBrackets_SkipsRecovery_WhenPttQxOrderWorking`, `TryReplacePttBeBrackets_SkipsRecovery_WhenPttQxOrderSubmitted`, `QueueBeRetryFallback_AttemptCounter_NotResetBeforeMoveStop`, `QueueBeRetryFallback_LoopTerminates_AfterCapAttempts`.
12. All 7 scans in the 7-Scan Checklist return zero violations.
13. `python scripts/complexity_audit.py` reports `TryReplacePttBeBrackets` CYC <= 8 and `QueueBeRetryFallback` CYC <= 8.
14. `dotnet build src/PropTraderTools/` exits with zero errors (F5-equivalent gate before sync).
15. Ticket completion report documents W1 resolution (`.ToList().Any()` chosen, reasoning stated).

---

### CYC Summary Table

| Method | File | Before | After | Delta | <= 8? |
|--------|------|--------|-------|-------|-------|
| `TryReplacePttBeBrackets` | CopyEngine.cs | 6 | 7 | +1 | YES |
| `QueueBeRetryFallback` (outer method) | CopyEngine.cs | 1 | 1 | 0 | YES |
| `QueueBeRetryFallback` timer tick lambda | CopyEngine.cs | 2 | 2 | 0 | YES |
| `TryFireFollowerBeRetry` (unchanged) | CopyEngine.cs | 5 | 5 | 0 | YES |
| `TryEvictFollowerBeSlot` (unchanged) | CopyEngine.cs | 6 | 6 | 0 | YES |
| `ExecuteOne` (PttGlobalQuickExit.cs, comment only) | PttGlobalQuickExit.cs | unchanged | unchanged | 0 | YES |
| `Execute` (PttBreakEvenSwap.cs, OUT OF SCOPE) | PttBreakEvenSwap.cs | 8 | 8 | 0 | YES |

**Pre-change CYC=6 note**: The method header comment in source currently reads `CYC=5` (predating DW-B92). The reviewer confirmed this annotation is stale — L2293 already has a 4th guard (`_qxCancelInProgress.ContainsKey`) added by DW-B105, making the true pre-B111 count 6. Change D updates the comment to `CYC=7`.

---

### JS Rules Compliance Reference (per ticket)

| Rule | Check | Status |
|------|-------|--------|
| JS-021 (No lock()) | No `lock(` in any changed line | REQUIRED — Scan 1 and Scan 4 verify |
| JS-033 (No async void) | No `async void` introduced | REQUIRED — Scan 2 and Scan 5 verify |
| JS-001 (No throw in hot paths) | No new exception throws | REQUIRED — both methods are void |
| JS-002 (No return null) | Both methods return void | REQUIRED — no return value |
| JS-036 (No heap alloc in hot path) | `.ToList()` produces one snapshot List<Order>; acceptable in `OnOrderUpdate` callback (not a sub-microsecond hot path) | ACCEPTABLE |
| ASCII-only | All new string literals are ASCII | REQUIRED — Scan 7 verifies |
| CYC <= 8 | `TryReplacePttBeBrackets` = 7, all others <= 8 | REQUIRED — Scan 6 verifies |
| PTT- prefix | No new `CreateOrder` calls | N/A — no new order submissions |
| DateTime.UtcNow | Not touched | N/A |

---

*Ticket generated by ptt-architect | Block B111-T1 | Phase 3 | 2026-08-28*
*Plan source: docs/brain/B111/02-architecture-plan.md (REVIEW_PASS)*
*Review source: docs/brain/B111/02-plan-review.md*
