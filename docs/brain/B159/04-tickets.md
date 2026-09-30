# DW-B159 — Tickets
<!-- status: TICKETS_COMPLETE -->
<!-- wave: B159 -->
<!-- epic: DW-B159 — Ghost Re-entry Guard + BE-All Silent Rollback Repair -->
<!-- phase: 3 -->
<!-- author: ptt-architect -->
<!-- plan-status: REVIEW_PASS (02-architecture-plan.md corrected, pass 3) -->

---

## DW-B159-T1: FM-A — Ghost Re-entry Guard

### Title
DW-B159-T1: Failure Mode A — Ghost Re-entry Guard

### Failure Mode
A

### Wave
B159

### Files Changed
`src/PropTraderTools/CopyEngine.cs` — **only this file**
Tests: `src/PropTraderTools/CopyEngineTests.cs`

### Root Cause
`ShouldSkipFollower` does not check whether the follower already holds an open
position on the instrument before copying a leader entry. When the leader
re-enters a trade in the same direction, a second copy order fires against the
follower's existing open position, silently doubling it ("ghost re-entry").

---

### New Symbols

| Name | Signature | CYC | Description |
|------|-----------|-----|-------------|
| `IsFollowerPositionBlocked` | `internal bool IsFollowerPositionBlocked(Account acc, Instrument instr)` | 3 | Returns `true` when `acc` has an open position on `instr`. Delegates to existing `HasOpenPosition` (line 5273). Null-guards both parameters (return `false`). |

**CYC breakdown — `IsFollowerPositionBlocked` (budget ≤8):**

| Branch | Delta |
|--------|-------|
| base | 1 |
| `acc == null` guard | +1 |
| `instr == null` guard | +1 |
| **Total** | **3** |

---

### Modified Symbols

| Method | Line | Change |
|--------|------|--------|
| `ShouldSkipFollower` | 3064 | Insert `if (IsFollowerPositionBlocked(acc, instr))` block as the **first** statement — before the existing `ShouldSkipFollowerDispatch` call. CYC rises 3 → 4. |

---

### Exact Insertion Points

#### New method `IsFollowerPositionBlocked` — insert BEFORE line 3056

Place the method immediately before the `ShouldSkipFollower` declaration at
line 3056. After insertion, `ShouldSkipFollower` shifts down by ~8 lines.

Surrounding lines for anchor (read-verified):

```
[line 3051]  // Extracted from DispatchCopy loop to reduce DispatchCopy CYC budget (V-01 fix: PTT-REPAIRS-03).
[line 3052]  // Short-circuits identically to the original two sequential ifs: ShouldSkipFollowerDispatch first,
[line 3053]  // then ShouldSkipForReversalGuard. Zero new logic.
[line 3054]  // CYC=3: base + dispatch-skip check + reversal-guard check.
[line 3055]  // JS-001: no throw. JS-021: no lock. JS-042: ASCII-only.
[line 3056]  private bool ShouldSkipFollower(   <-- INSERT NEW METHOD ABOVE THIS LINE
```

Insert the following block immediately before line 3056:

```csharp
        // B159 DW-B159-FM-A: returns true when acc already holds an open position on instr.
        // Delegates to HasOpenPosition (line 5273) -- no duplicate logic.
        // CYC=3: base(1) + acc-null guard(2) + instr-null guard(3).
        // JS-001: no throw. JS-021: no lock. JS-042: ASCII-only.
        // internal: accessible to xUnit via InternalsVisibleTo.
        internal bool IsFollowerPositionBlocked(Account acc, Instrument instr)
        {
            if (acc == null)   return false;    // (2)
            if (instr == null) return false;    // (3)
            return HasOpenPosition(acc, instr);
        }

```

#### Hook in `ShouldSkipFollower` — first statement at line 3064

After the new method is inserted, `ShouldSkipFollower`'s opening brace is at
approximately line 3063 (shifted). The **first** executable line is currently:

```
[line 3064]  if (ShouldSkipFollowerDispatch(acc))   <-- INSERT NEW BLOCK BEFORE THIS LINE
[line 3065]      return true;
```

Insert the following block as the very first statement inside
`ShouldSkipFollower`, before the `ShouldSkipFollowerDispatch` call:

```csharp
            // B159 DW-B159-FM-A: ghost re-entry guard -- block copy when follower already has position.
            if (IsFollowerPositionBlocked(acc, instr))
            {
                NinjaTrader.Code.Output.Process(
                    "[PTT-COPY-GUARD] ghost-entry blocked: " + acc.Name + " " + instr.FullName,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
                return true;
            }
```

Final shape of `ShouldSkipFollower` after modification:

```csharp
        private bool ShouldSkipFollower(
            Account acc,
            NinjaTrader.Cbi.Instrument instr,
            OrderAction currentAction,
            OrderAction lastAction,
            bool hasLastDirection
        )
        {
            // B159 DW-B159-FM-A: ghost re-entry guard -- block copy when follower already has position.
            if (IsFollowerPositionBlocked(acc, instr))
            {
                NinjaTrader.Code.Output.Process(
                    "[PTT-COPY-GUARD] ghost-entry blocked: " + acc.Name + " " + instr.FullName,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
                return true;
            }
            if (ShouldSkipFollowerDispatch(acc))
                return true;
            if (ShouldSkipForReversalGuard(acc, instr, currentAction, lastAction, hasLastDirection))
                return true;
            return false;
        }
```

---

### Tests (TDD — red-green format)

All tests added to `src/PropTraderTools/CopyEngineTests.cs`.

```csharp
[Fact]
public void ShouldSkipFollower_FollowerHasOpenPosition_ReturnsTrue()
// Arrange: mock acc with HasOpenPosition -> true, dispatch not blocked.
// Act: ShouldSkipFollower(acc, instr, ...).
// Assert: result == true (first guard fires, copy is suppressed).

[Fact]
public void ShouldSkipFollower_FollowerNoOpenPosition_ReturnsFalse()
// Arrange: mock acc with HasOpenPosition -> false, dispatch not blocked, no reversal.
// Act: ShouldSkipFollower(acc, instr, ...).
// Assert: result == false (first guard passes through, existing guards also clear).

[Fact]
public void IsFollowerPositionBlocked_NullAccount_ReturnsFalse()
// Arrange: acc = null, instr = valid instrument stub.
// Act: IsFollowerPositionBlocked(null, instr).
// Assert: result == false (no NullReferenceException, guard returns false).

[Fact]
public void IsFollowerPositionBlocked_NullInstrument_ReturnsFalse()
// Arrange: acc = valid account stub, instr = null.
// Act: IsFollowerPositionBlocked(acc, null).
// Assert: result == false (no NullReferenceException, guard returns false).

[Fact]
public void IsFollowerPositionBlocked_AccountWithNoPosition_ReturnsFalse()
// Arrange: non-null acc with Qty=0 (flat) -- HasOpenPosition returns false.
// Act: IsFollowerPositionBlocked(acc, instr).
// Assert: result == false (flat account is not blocked).

[Fact]
public void IsFollowerPositionBlocked_AccountWithOpenPosition_ReturnsTrue()
// Arrange: non-null acc with Qty>0 (open) -- HasOpenPosition returns true.
// Act: IsFollowerPositionBlocked(acc, instr).
// Assert: result == true; [PTT-COPY-GUARD] log line emitted.
```

---

### CYC Budget

| Method | Before | After | Budget |
|--------|--------|-------|--------|
| `IsFollowerPositionBlocked` (new) | — | 3 | ≤8 ✓ |
| `ShouldSkipFollower` (modified) | 3 | 4 | ≤8 ✓ |

---

### 7-Scan Checklist

| Scan | Pattern | Expected result |
|------|---------|-----------------|
| SCAN-01 | `grep "lock(" src/PropTraderTools/CopyEngine.cs` | Zero matches in new/modified code |
| SCAN-02 | `grep "async void" src/PropTraderTools/CopyEngine.cs` | Zero matches in new/modified code |
| SCAN-03 | `grep "DateTime.Now" src/PropTraderTools/CopyEngine.cs` | Zero matches in new/modified code |
| SCAN-04 | `grep "Thread.Sleep" src/PropTraderTools/CopyEngine.cs` | Zero matches in new/modified code |
| SCAN-05 | `grep -P "[\x80-\xFF]" src/PropTraderTools/CopyEngine.cs` | Zero matches — ASCII-only strings in all new code |
| SCAN-06 | CYC audit: `IsFollowerPositionBlocked`=3, `ShouldSkipFollower`=4 | All new/modified methods ≤8 |
| SCAN-07 | `grep "acc\.Orders" src/PropTraderTools/CopyEngine.cs` (new code only) | Zero `acc.Orders` calls without `.ToList()` in new/modified code |

---

### VERIFY Criterion (SIM Gate)

After implementing, run a SIM session where follower accounts Sim102 and Sim103
already hold open positions before a new leader entry is dispatched.

```
grep "\[PTT-COPY-GUARD\] ghost-entry blocked" docs/output.md
```

**Required:** At least one match per follower account (Sim102 and Sim103) during
the first-cycle test where positions are open.

**Failure signal:** If `DispatchCopy` fires for Sim102/Sim103 while they hold
open positions and no `[PTT-COPY-GUARD]` line appears, the hook is missing or
misfiring.

---

### SYNC Requirement

After every `.cs` edit:

```
powershell -File .\scripts\ptt-sync.ps1
```

Hard-link re-synchronization is mandatory. Do not skip.

### DIFF GUARD

Before push:

```
powershell -File .\deploy-sync.ps1
```

Diff must be less than 10,000 characters of source changes in `src/`. If DIFF
GUARD fails, isolate logic changes and revert whitespace/artifact bloat before
proceeding.

---

---

## DW-B159-T2: FM-B — BE-All Silent Rollback Repair

### Title
DW-B159-T2: Failure Mode B — BE-All Silent Rollback Repair

### Failure Mode
B

### Wave
B159

### Files Changed
`src/PropTraderTools/CopyEngine.cs` — **only this file**
Tests: `src/PropTraderTools/CopyEngineTests.cs`

### Root Cause
After a QX→BE-All cycle, `MoveStopToBreakEven` places a `PTT-BE-Stop-*` order.
When NT8's ATM strategy subsequently cancels that order (bracket wipe), the
existing `TryTriggerBeRecovery` path re-places brackets but has no mechanism to
remember and re-apply the original target BE price, resulting in silent rollback
of the follower's break-even stop. Additionally, a stale retry-slot leak existed
when the stop successfully transitioned to `Working` (V-06 gap).

---

### New Fields

Insert both fields **after line 322** — immediately after the
`_beReplaceAttempts` declaration, before `_qxCancelInProgress` at line 324.

Surrounding lines for anchor (read-verified):

```
[line 321]          private readonly ConcurrentDictionary<string, int> _beReplaceAttempts =
[line 322]              new ConcurrentDictionary<string, int>();
[line 323]                                                         <-- INSERT HERE
[line 324]          // DW-B105: QX-ALL intent guard. Set per follower account by PttGlobalQuickExit.ExecuteOne
[line 325]          // before CancelQxBrackets, cleared after. TryReplacePttBeBrackets returns early if set.
[line 326]          // ConcurrentDictionary: JS-021 lock-free. Key = acc.Name (string). Value = bool (unused).
[line 327]          internal readonly ConcurrentDictionary<string, bool> _qxCancelInProgress =
```

Insert:

```csharp
        // B159 DW-B159-FM-B: per-account target BE price for retry.
        // Key = acc.Name. Value = bePrice stored at TryArmBePriceRetry time.
        // StringComparer.Ordinal: deterministic key match, consistent with JS-021.
        // JS-021: ConcurrentDictionary -- lock-free TryAdd/TryGetValue/TryRemove. No lock() anywhere.
        private readonly ConcurrentDictionary<string, double> _pendingBePriceRetry =
            new ConcurrentDictionary<string, double>(StringComparer.Ordinal);

        // B159 DW-B159-FM-B: retry attempt counter per account.
        // Prevents unbounded retry storm. Max attempts = 3 per trade cycle.
        // Reset by TryFireBePriceRetry eviction logic.
        // Key = acc.Name. JS-021: ConcurrentDictionary -- lock-free. JS-023: int values.
        private readonly ConcurrentDictionary<string, int> _bePriceRetryAttempts =
            new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

```

---

### New Symbols

| Name | Signature | CYC | Description |
|------|-----------|-----|-------------|
| `TryArmBePriceRetry` | `internal void TryArmBePriceRetry(Account acc, Instrument instr, double bePrice)` | 5 | Registers a pending BE price retry slot. Called from `MoveStopToBreakEven` after `PttBreakEvenSwap.Execute`. Ceiling: 3 attempts per account. |
| `StillAtNonBePrice` | `internal bool StillAtNonBePrice(Account acc, Instrument instr, double bePrice)` | 7 | Returns `true` when a working `PTT-BE-Stop-*` for the instrument exists at a price different from `bePrice`, or when no working stop is found at all (needs BE placement). Returns `false` when stop is already at `bePrice`. |
| `TryFireBePriceRetry` | `internal void TryFireBePriceRetry(Order o)` | 7 | Fires deferred BE stop on `Cancelled` path; evicts stale retry slot on `Working` path (V-06 fix). Hook into `OnOrderUpdate` after `TryRecordBeTargetFill`. |

---

**CYC breakdown — `TryArmBePriceRetry` (budget ≤8):**

| Branch | Delta |
|--------|-------|
| base | 1 |
| `acc == null` guard | +1 |
| `instr == null` guard | +1 |
| `bePrice <= 0.0` guard | +1 |
| `attempts >= 3` ceiling | +1 |
| **Total** | **5** |

**CYC breakdown — `StillAtNonBePrice` (budget ≤8):**

| Branch | Delta |
|--------|-------|
| base | 1 |
| `acc == null` guard | +1 |
| `instr == null` guard | +1 |
| `foreach` loop decision | +1 |
| name null + StartsWith compound guard | +1 |
| instrument FullName match | +1 |
| `OrderState.Working` state guard | +1 |
| **Total** | **7** |

**CYC breakdown — `TryFireBePriceRetry` (budget ≤8):**

| Branch | Delta |
|--------|-------|
| base | 1 |
| compound null guard (`o` / `o.Name` / `o.Account`) | +1 |
| `!o.Name.StartsWith("PTT-BE-Stop-")` | +1 |
| `!_pendingBePriceRetry.TryGetValue` (no slot) | +1 |
| `o.OrderState == Working` cleanup path (V-06) | +1 |
| `o.OrderState != Cancelled` early return | +1 |
| `!StillAtNonBePrice` early return | +1 |
| **Total** | **7** |

---

### Modified Symbols

| Method | Line | Change |
|--------|------|--------|
| `MoveStopToBreakEven` | 6857 | Insert `TryArmBePriceRetry(acc, instrument, newStop);` as 1 call AFTER the `StatusUpdate?.Invoke(...)` line (line 6857), before the `if (targets.Count == 0)` block (line 6860). |
| `OnOrderUpdate` | 1725 | Insert `TryFireBePriceRetry(e.Order);` as 1 call AFTER `TryRecordBeTargetFill(e.Order)` (line 1725) and BEFORE `TryTriggerBeRecovery(e.Order)` (line 1726). |

---

### Exact Insertion Points

#### Hook 1 — `TryArmBePriceRetry` call in `MoveStopToBreakEven` (after line 6857)

Surrounding lines for anchor (read-verified):

```
[line 6856]          PttBreakEvenSwap.Execute(acc, instrument, newStop, targets);
[line 6857]          StatusUpdate?.Invoke(acc.Name + ": BE stop @ " + newStop);
[line 6858]                                                         <-- INSERT HERE
[line 6859]          // DW-B79-06 + DW-B79-07: extracted retry slot registration to RegisterBeRetrySlotIfNeeded.
[line 6860]          if (targets.Count == 0)
```

Insert immediately after line 6857:

```csharp
            // B159 DW-B159-FM-B: arm BE price retry in case NT8-ATM cancels the new stop.
            TryArmBePriceRetry(acc, instrument, newStop);
```

#### Hook 2 — `TryFireBePriceRetry` call in `OnOrderUpdate` (between lines 1725 and 1726)

Surrounding lines for anchor (read-verified):

```
[line 1724]          // DW-B92: record PTT-BE-Target-* fill BEFORE OCO partner cancel arrives.
[line 1725]          TryRecordBeTargetFill(e.Order);
[line 1725a]                                                        <-- INSERT HERE
[line 1726]          TryTriggerBeRecovery(e.Order);
```

Insert between lines 1725 and 1726:

```csharp
            // B159 DW-B159-FM-B: retry deferred BE stop (Cancelled path) or evict slot (Working path).
            TryFireBePriceRetry(e.Order);
```

Final call order in `OnOrderUpdate` after this change:

```
[line 1725]   TryRecordBeTargetFill(e.Order);     // already present -- no change
[line 1725a]  TryFireBePriceRetry(e.Order);       // NEW: inserted here
[line 1726]   TryTriggerBeRecovery(e.Order);      // already present -- no change
```

Rationale: `TryFireBePriceRetry` must evaluate order state before
`TryTriggerBeRecovery` may modify or re-place brackets. `TryRecordBeTargetFill`
at line 1725 runs first so position state is accurate when
`TryFireBePriceRetry` calls `StillAtNonBePrice`.

#### New methods — placement (near end of class, after `MoveStopToBreakEven` body)

Insert the three new methods as a contiguous block near line 6880
(after `RegisterBeRetrySlotIfNeeded` and the `DW-B88 LEGACY` comment block that
ends around line 6940). Exact neighboring anchor — insert before the next
`private`/`internal` method declaration that follows the `MoveStopToBreakEven`
region.

**`TryArmBePriceRetry`:**

```csharp
        // B159 DW-B159-FM-B: register a pending BE price retry slot for acc+instr.
        // Called from MoveStopToBreakEven (line 6816) when a BE stop placement may need retry.
        // CYC=5: base(1) + acc-null(2) + instr-null(3) + bePrice-guard(4) + attempt-ceiling(5).
        // Max 3 attempts per account per trade. StringComparer.Ordinal: JS-021.
        // JS-001: no throw. JS-021: no lock. JS-042: ASCII-only. No DateTime.Now.
        internal void TryArmBePriceRetry(Account acc, Instrument instr, double bePrice)
        {
            if (acc == null)    return;                                    // (2)
            if (instr == null)  return;                                    // (3)
            if (bePrice <= 0.0) return;                                    // (4)
            int attempts = _bePriceRetryAttempts.GetOrAdd(acc.Name, 0);
            if (attempts >= 3)  return;                                    // (5) ceiling guard
            _pendingBePriceRetry[acc.Name] = bePrice;
            _bePriceRetryAttempts.AddOrUpdate(acc.Name, 1, (_, prev) => prev + 1);
            NinjaTrader.Code.Output.Process(
                "[PTT-BE-RETRY] armed: " + acc.Name + " bePrice=" + bePrice,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
        }
```

**`StillAtNonBePrice`:**

```csharp
        // B159 DW-B159-FM-B: returns true when acc has a working PTT-BE-Stop-* order
        // whose price differs from bePrice (needs retry). Returns true if no working stop found
        // (no stop at all = still needs BE). Returns false when stop is already at bePrice.
        // CYC=7: base(1) + acc-null(2) + instr-null(3) + foreach(4) +
        //        name-guard(5) + instr-match(6) + state-guard(7).
        // JS-001: no throw. JS-021: no lock (acc.Orders is NT8 read-only). JS-042: ASCII-only.
        internal bool StillAtNonBePrice(Account acc, Instrument instr, double bePrice)
        {
            if (acc == null)   return false;                                              // (2)
            if (instr == null) return false;                                              // (3)
            foreach (var o in acc.Orders.ToList())                                        // (4) JS-021: snapshot live collection
            {
                if (o.Name == null
                    || !o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal))
                    continue;                                                             // (5)
                if (o.Instrument?.FullName != instr.FullName) continue;                  // (6)
                if (o.OrderState != OrderState.Working)        continue;                 // (7)
                double tick = instr.MasterInstrument.TickSize;
                return Math.Abs(o.StopPrice - bePrice) > tick * 0.5;
            }
            return true; // no working stop found -- still needs BE
        }
```

**`TryFireBePriceRetry`:**

```csharp
        // B159 DW-B159-FM-B: attempt to fire deferred BE stop when PTT-BE-Stop-* is Cancelled
        // (bracket wipe recovery path). Also cleans up retry slot when PTT-BE-Stop-* goes Working
        // (V-06 fix: Option a -- Working state treated as successful placement, evict slot).
        // CYC=7: base(1) + compound-null-guard(2) + name-prefix(3) + no-slot(4) +
        //        working-cleanup(5) + not-cancelled(6) + still-at-non-be(7).
        // JS-001: no throw. JS-021: no lock (ConcurrentDictionary TryRemove is lock-free).
        // JS-042: ASCII-only. No DateTime.Now. "PTT-" prefix per order naming standard.
        internal void TryFireBePriceRetry(Order o)
        {
            if (o == null || o.Name == null || o.Account == null)                     // (2)
                return;
            if (!o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal))        // (3)
                return;
            if (!_pendingBePriceRetry.TryGetValue(o.Account.Name, out double bePrice)) // (4)
                return;

            // V-06 fix (Option a): Working state = stop successfully placed -- evict slot.
            if (o.OrderState == OrderState.Working)                                   // (5)
            {
                _pendingBePriceRetry.TryRemove(o.Account.Name, out _);
                _bePriceRetryAttempts.TryRemove(o.Account.Name, out _);
                NinjaTrader.Code.Output.Process(
                    "[PTT-BE-RETRY] working-cleanup: " + o.Account.Name,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
                return;
            }

            if (o.OrderState != OrderState.Cancelled)                                 // (6)
                return;
            if (!StillAtNonBePrice(o.Account, o.Instrument, bePrice))                // (7)
                return;

            // Fire: submit BE stop at stored price.
            var pos = FindPosition(o.Account, o.Instrument);
            bool isLong = pos?.MarketPosition == MarketPosition.Long;
            SubmitBeStop(o.Account, o.Instrument, bePrice, isLong);    // line 1389
            _pendingBePriceRetry.TryRemove(o.Account.Name, out _);
            _bePriceRetryAttempts.TryRemove(o.Account.Name, out _);
            NinjaTrader.Code.Output.Process(
                "[PTT-BE-RETRY] fired: " + o.Account.Name + " bePrice=" + bePrice,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
        }
```

---

### Tests (TDD — red-green format)

All tests added to `src/PropTraderTools/CopyEngineTests.cs`.

```csharp
[Fact]
public void TryFireBePriceRetry_CancelledOrderWithRetryKey_HasOpenPosition_StillAtNonBePrice_CallsSubmitBeStop()
// Arrange: PTT-BE-Stop-* order in Cancelled state; entry exists in _pendingBePriceRetry;
//          StillAtNonBePrice returns true (stop not yet at BE).
// Act: TryFireBePriceRetry(order).
// Assert: SubmitBeStop was called once; _pendingBePriceRetry and _bePriceRetryAttempts
//         entries for acc.Name are removed.

[Fact]
public void TryFireBePriceRetry_CancelledOrderWithRetryKey_AlreadyAtBePrice_NoSubmitBeStop()
// Arrange: PTT-BE-Stop-* order in Cancelled state; entry exists in _pendingBePriceRetry;
//          StillAtNonBePrice returns false (stop already at BE price).
// Act: TryFireBePriceRetry(order).
// Assert: SubmitBeStop was NOT called.

[Fact]
public void TryArmBePriceRetry_AttemptsAtCap_NoNewEntry()
// Arrange: _bePriceRetryAttempts[acc.Name] = 3 (at ceiling).
// Act: TryArmBePriceRetry(acc, instr, 4825.0).
// Assert: _pendingBePriceRetry does NOT contain a new entry for acc.Name;
//         _bePriceRetryAttempts stays at 3 (ceiling not exceeded).

[Fact]
public void TryFireBePriceRetry_WorkingOrder_RemovesFromDict()
// Arrange: PTT-BE-Stop-* order in Working state; entry exists in both dicts (V-06 scenario).
// Act: TryFireBePriceRetry(order).
// Assert: _pendingBePriceRetry and _bePriceRetryAttempts entries for acc.Name are removed;
//         SubmitBeStop was NOT called (stop already placed -- evict only).

[Fact]
public void TryFireBePriceRetry_NonPttBeStopPrefix_ExitsEarly()
// Arrange: Order with Name = "PTT-QX-Stop-1" (does not start with "PTT-BE-Stop-").
// Act: TryFireBePriceRetry(order).
// Assert: _pendingBePriceRetry is unchanged; SubmitBeStop was NOT called.

[Fact]
public void TryArmBePriceRetry_ValidInputsFirstAttempt_AddsEntryToDict()
// Arrange: valid cancelledOrder, IsFollowerAccount=true, HasOpenPosition=true, attempts=0.
// Act: TryArmBePriceRetry(acc, instr, 4825.0).
// Assert: _pendingBePriceRetry[acc.Name] == 4825.0;
//         _bePriceRetryAttempts[acc.Name] == 1.

[Fact]
public void TryFireBePriceRetry_CancelledPttBeStop_KeyNotInDict_NoSubmitBeStop()
// Arrange: PTT-BE-Stop-* order in Cancelled state; key NOT present in _pendingBePriceRetry.
// Act: TryFireBePriceRetry(order).
// Assert: TryGetValue fails; SubmitBeStop was NOT called; no exception thrown.

[Fact]
public void StillAtNonBePrice_WorkingStopAtOriginalPrice_ReturnsTrue()
// Arrange: acc has a working PTT-BE-Stop-* order for the instrument at price != bePrice.
// Act: StillAtNonBePrice(acc, instr, bePrice).
// Assert: returns true (stop exists but is not yet at the intended BE price).
```

---

### CYC Budget

| Method | Before | After | Budget |
|--------|--------|-------|--------|
| `TryArmBePriceRetry` (new) | — | 5 | ≤8 ✓ |
| `StillAtNonBePrice` (new) | — | 7 | ≤8 ✓ |
| `TryFireBePriceRetry` (new) | — | 7 | ≤8 ✓ |
| `MoveStopToBreakEven` (hook +1 call) | 4 | 4 | ≤8 ✓ |
| `OnOrderUpdate` (hook +1 call) | unchanged | unchanged | ≤8 ✓ |

---

### 7-Scan Checklist

| Scan | Pattern | Expected result |
|------|---------|-----------------|
| SCAN-01 | `grep "lock(" src/PropTraderTools/CopyEngine.cs` | Zero matches in new/modified code — all state via ConcurrentDictionary |
| SCAN-02 | `grep "async void" src/PropTraderTools/CopyEngine.cs` | Zero matches in new/modified code |
| SCAN-03 | `grep "DateTime.Now" src/PropTraderTools/CopyEngine.cs` | Zero matches in new/modified code |
| SCAN-04 | `grep "Thread.Sleep" src/PropTraderTools/CopyEngine.cs` | Zero matches in new/modified code |
| SCAN-05 | `grep -P "[\x80-\xFF]" src/PropTraderTools/CopyEngine.cs` | Zero matches — ASCII-only strings in all new code |
| SCAN-06 | CYC audit: `TryArmBePriceRetry`=5, `StillAtNonBePrice`=7, `TryFireBePriceRetry`=7 | All new/modified methods ≤8 |
| SCAN-07 | `grep "acc\.Orders" src/PropTraderTools/CopyEngine.cs` (new code only) | `StillAtNonBePrice` uses `acc.Orders.ToList()` — snapshot pattern confirmed; zero bare `acc.Orders` enumerations |

---

### VERIFY Criterion (SIM Gate)

Run a SIM session that exercises the QX→BE-All cycle followed by an ATM bracket
cancel for Sim104.

**Required — fire path:**

```
grep "\[PTT-BE-RETRY\] fired:" docs/output.md
```

Must appear for Sim104 after a `PTT-BE-Stop-*` bulk cancel event.

**Required — no silent rollback:**

```
grep "\[BE-DIAG-CANCEL\]" docs/output.md
```

Every `[BE-DIAG-CANCEL]` line for a `PTT-BE-Stop-*` order belonging to Sim104
must be followed by a corresponding `[PTT-BE-RETRY] fired:` or
`[PTT-BE-RETRY] working-cleanup:` line. An `[BE-DIAG-CANCEL]` with no
subsequent `[PTT-BE-RETRY]` entry is a FAILURE signal.

**Required — arm path:**

```
grep "\[PTT-BE-RETRY\] armed:" docs/output.md
```

Must appear for Sim104 immediately after `MoveStopToBreakEven` fires during the
QX→BE-All trigger.

**Required — Working cleanup path (V-06):**

```
grep "\[PTT-BE-RETRY\] working-cleanup:" docs/output.md
```

Must appear when a `PTT-BE-Stop-*` order for Sim104 successfully transitions to
`Working` state, confirming slot eviction.

---

### SYNC Requirement

After every `.cs` edit:

```
powershell -File .\scripts\ptt-sync.ps1
```

Hard-link re-synchronization is mandatory. Do not skip.

### DIFF GUARD

Before push:

```
powershell -File .\deploy-sync.ps1
```

Diff must be less than 10,000 characters of source changes in `src/`. If DIFF
GUARD fails, isolate logic changes and revert whitespace/artifact bloat before
proceeding.

---

## Spec Requirements Satisfied

| Req ID | Satisfied by |
|--------|-------------|
| FM-A: ghost re-entry suppression | DW-B159-T1 (`IsFollowerPositionBlocked` + `ShouldSkipFollower` hook) |
| FM-B: BE price retry on ATM-cancel | DW-B159-T2 (`TryArmBePriceRetry`, `TryFireBePriceRetry`, field pair) |
| FM-B V-06: Working-state slot eviction | DW-B159-T2 (`TryFireBePriceRetry` branch 5) |
| FM-B: attempt ceiling (anti-storm) | DW-B159-T2 (`TryArmBePriceRetry` branch 5 — max 3) |
| FM-B: safe `acc.Orders` enumeration | DW-B159-T2 (`StillAtNonBePrice` — `.ToList()` snapshot) |
| No `lock()` anywhere | Both tickets — ConcurrentDictionary throughout |
| ASCII-only log strings | Both tickets — all log markers verified |
| `"PTT-"` prefixed order names | DW-B159-T2 — `PTT-BE-Stop-*` prefix in all guards |
| No `DateTime.Now` | Both tickets — no date usage in new code |
| CYC ≤8 all new methods | Both tickets — verified in CYC budget tables |
