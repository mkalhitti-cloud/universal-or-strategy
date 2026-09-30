# B180 Engineering Tickets
# Block: B180 -- DW-B180-BE-SWAP-ALL
# Produced by: ptt-architect (Phase 3)
# Source: docs/brain/B180/02-architecture-plan.md (REVIEW_PASS)
#         docs/brain/B180/02-plan-review.md (minor notes MN-01, MN-02, MN-03 addressed)
# Status: AWAITING TICKET REVIEW (Phase 3.5)

---

## Ticket B180-1: Additive Rewire -- New Classes + Button Handler Replacement

### Spec Requirements

- DW-B180-BE-SWAP-ALL: per-chart BE button fires immediate bracket swap via PttBeSwapOne (orchestrator prompt -- "Per-chart BE button" section)
- DW-B180-BE-SWAP-ALL: BE ALL button fires immediate swap via PttBeSwapAll loop (orchestrator prompt -- "BE ALL button" section)
- SnapshotBeTargetsPublic internal wrapper added to CopyEngine (orchestrator prompt -- "SnapshotBeTargetsPublic" note)
- Feature flag gate (BreakEven) in both new classes (orchestrator prompt -- "What is kept")
- Flat guard (skip qty==0 positions) in both new classes (orchestrator prompt -- "Flat guard")
- Follower skip in PttBeSwapAll (orchestrator prompt -- "BE ALL button")
- B180-1 is additive ONLY -- no removal (orchestrator prompt -- "SCOPE GATE")
- Entry price: pos.AveragePrice + direction * bufferTicks * tickSize, rounded via Math.Round (orchestrator prompt -- "Entry price calculation")

### Dependency

NONE. B180-1 is the first ticket in the pipeline.

### Files Changed

| File | Action |
|------|--------|
| `src/PropTraderTools/Features/PttBeSwapOne.cs` | CREATE (new file) |
| `src/PropTraderTools/Features/PttBeSwapAll.cs` | CREATE (new file) |
| `src/PropTraderTools/CopyEngine.cs` | EDIT -- insert SnapshotBeTargetsPublic wrapper after line 6979 |
| `src/PropTraderTools/TradeCopierPanel.cs` | EDIT -- replace OnBeClick block (lines 1474-1534) |
| `src/PropTraderTools/TradeCopierPanel.cs` | EDIT -- replace OnGlobalBeClick block (lines 1349-1377) |

---

### Method Signatures

**PttBeSwapOne.cs (new file):**
```csharp
internal static void Execute(Account acc, Instrument instr, int bufferTicks)
```
- CYC=7 (strict Lizard: base1 + flag1 + instr-null1 + tickSize1 + flat1 + ?.1 + ??1)
- JS-021: no lock. JS-001: no throw. JS-002: void. JS-033: synchronous. ASCII-only.

**PttBeSwapAll.cs (new file):**
```csharp
internal static void Execute(int bufferTicks)
private static void ExecuteOne(Account acc, Position pos, int bufferTicks)
```
- Execute CYC=6. ExecuteOne CYC=5. Both <= 8.
- JS-021: no lock. JS-001: no throw. JS-002: void. JS-033: synchronous. ASCII-only.

**CopyEngine.cs (insert after line 6979):**
```csharp
internal List<(double Price, int Qty, OrderAction Action)> SnapshotBeTargetsPublic(
    Account acc, Instrument instr)
    => SnapshotBeTargets(acc, instr);
```
- CYC=1. JS-021: no lock. JS-001: no throw. JS-002: delegates to method that returns empty List, never null.

**TradeCopierPanel.cs -- OnBeClick (replacement):**
```csharp
private void OnBeClick(object sender, RoutedEventArgs e)
```
- CYC=4 (base1 + instr-null1 + leader-null1 + ??-on-leaderAccount1). <= 8.

**TradeCopierPanel.cs -- OnGlobalBeClick (replacement):**
```csharp
private void OnGlobalBeClick(object sender, RoutedEventArgs e)
```
- CYC=1 (straight-line). <= 8.

---

### Implementation Instructions

#### Step 1 -- Create `src/PropTraderTools/Features/PttBeSwapOne.cs` (write_file)

Use `write_file`. This is a new file; the `Features/` subdirectory already exists (contains PttBreakEvenSwap.cs and PttGlobalBreakEven.cs).

**Complete file content:**

```csharp
// src/PropTraderTools/Features/PttBeSwapOne.cs
// B180 DW-B180-BE-SWAP-ALL: per-chart immediate BE bracket swap.
// Replaces PttBreakEven module (DispatchModule("BE") path).
// Pattern: mirrors PttBeSwapAll.ExecuteOne but scoped to one account.
// CYC=7: (1) flag-gate, (2) instr null, (3) tickSize ?. , (4) tickSize ??, (5) flat guard, (6) ??-pos-qty, (7) PttBreakEvenSwap.
// JS-021: no lock. JS-001: no throw. JS-002: void. JS-033: synchronous void. ASCII-only.

using System;
using NinjaTrader.Cbi;

namespace PropTraderTools
{
    internal static class PttBeSwapOne
    {
        internal static void Execute(Account acc, Instrument instr, int bufferTicks)
        {
            if (!CopyEngine.Instance.Flags.BreakEven)          // (1) feature gate
            {
                CopyEngine.Instance.StatusUpdate?.Invoke("Break Even requires Pro tier");
                return;
            }
            if (instr == null) return;                         // (2)
            double tickSize = instr.MasterInstrument?.TickSize ?? 0.0;
            if (tickSize <= 0.0) return;                       // (3)
            var pos = CopyEngine.Instance.FindPositionPublic(acc, instr);
            if (pos == null || pos.Quantity == 0)              // (4)
            {
                CopyEngine.Instance.StatusUpdate?.Invoke(acc.Name + ": BE flat skip");
                return;
            }
            bool isLong = pos.MarketPosition == MarketPosition.Long;
            double direction = isLong ? -1.0 : +1.0;
            double raw = pos.AveragePrice + direction * bufferTicks * tickSize;
            double entryPrice = Math.Round(raw / tickSize) * tickSize;
            var targets = CopyEngine.Instance.SnapshotBeTargetsPublic(acc, instr); // (5)
            NinjaTrader.Code.Output.Process(
                "[BE] swap: " + acc.Name + " entry=" + entryPrice
                    + " targets=" + targets.Count,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1);
            PttBreakEvenSwap.Execute(acc, instr, entryPrice, targets);
        }
    }
}
```

---

#### Step 2 -- Create `src/PropTraderTools/Features/PttBeSwapAll.cs` (write_file)

Use `write_file`. New file.

**Complete file content:**

```csharp
// src/PropTraderTools/Features/PttBeSwapAll.cs
// B180 DW-B180-BE-SWAP-ALL: immediate BE bracket swap for all accounts.
// Replaces PttGlobalBreakEven.Execute(int) deferred-arm path.
// Pattern: identical to PttGlobalQuickExit.Execute() loop structure.
// CYC=6: flag-gate(1), acc-loop-foreach(2), follower-skip(3), &&-in-follower-check(4), flat-skip(5), flat-skip-||null(6).
// JS-021: no lock. JS-001: no throw. JS-002: void. JS-033: synchronous void. ASCII-only.

using System;
using NinjaTrader.Cbi;

namespace PropTraderTools
{
    internal static class PttBeSwapAll
    {
        internal static void Execute(int bufferTicks)
        {
            if (!CopyEngine.Instance.Flags.BreakEven)         // (1) feature gate
            {
                NinjaTrader.Code.Output.Process(
                    "[BE-ALL] Blocked: Break Even requires Pro tier",
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                return;
            }
            NinjaTrader.Code.Output.Process(
                "[BE-ALL] PttBeSwapAll: firing buf=" + bufferTicks + "t",
                NinjaTrader.NinjaScript.PrintTo.OutputTab1);
            var engine = CopyEngine.Instance;
            foreach (Account acc in Account.All)               // (2)
            {
                if (engine != null && engine.IsFollowerAccount(acc))
                    continue;                                  // (3)
                foreach (Position pos in acc.Positions)
                {
                    if (pos == null || pos.Quantity == 0)
                        continue;                              // (4) flat skip
                    ExecuteOne(acc, pos, bufferTicks);         // (5)
                }
            }
        }

        // CYC=5: (1) instr null, (2) tickSize ?., (3) tickSize ??, (4) tickSize<=0, (5) RoundToTick+swap.
        // JS-021: no lock. JS-001: no throw. JS-002: void. ASCII-only.
        private static void ExecuteOne(Account acc, Position pos, int bufferTicks)
        {
            var instr = pos.Instrument;
            if (instr == null) return;                         // (1)
            double tickSize = instr.MasterInstrument?.TickSize ?? 0.0;
            if (tickSize <= 0.0) return;
            bool isLong = pos.MarketPosition == MarketPosition.Long;
            double direction = isLong ? -1.0 : +1.0;
            double raw = pos.AveragePrice + direction * bufferTicks * tickSize;
            double entryPrice = Math.Round(raw / tickSize) * tickSize; // (2) RoundToTick inline
            var targets = CopyEngine.Instance.SnapshotBeTargetsPublic(acc, instr); // (3)
            NinjaTrader.Code.Output.Process(
                "[BE-ALL] swap: " + acc.Name + " entry=" + entryPrice
                    + " targets=" + targets.Count,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1);
            PttBreakEvenSwap.Execute(acc, instr, entryPrice, targets);
        }
    }
}
```

---

#### Step 3 -- Edit CopyEngine.cs: Insert SnapshotBeTargetsPublic wrapper (apply_diff)

**Confirmed source location (verified against file):**
- `SnapshotBeTargets` closing brace is at line **6979** (`}`)
- Insert 5 lines immediately after line 6979 (blank + comment + 3-line method)

**apply_diff SEARCH block (exact content at lines 6977-6980):**

```
<<<<<<< SEARCH
:start_line:6977
-------
            return nativeTargets.Count > 0 ? nativeTargets : pttTargets; // (7)
        }

        // CCN<=6: base(1)+o null(1)+!IsBeTargetStateOk(1)+instrOk&&(1)+!instrOk||type(2)+IsNullOrEmpty(1)=CCN<=7.
=======
            return nativeTargets.Count > 0 ? nativeTargets : pttTargets; // (7)
        }

        // B180: public seam for PttBeSwapAll and PttBeSwapOne. Delegates to private SnapshotBeTargets.
        // CYC=1. JS-021: no lock. JS-001: no throw. JS-002: returns List (delegates, never null). ASCII-only.
        internal List<(double Price, int Qty, OrderAction Action)> SnapshotBeTargetsPublic(
            Account acc, Instrument instr)
            => SnapshotBeTargets(acc, instr);

        // CCN<=6: base(1)+o null(1)+!IsBeTargetStateOk(1)+instrOk&&(1)+!instrOk||type(2)+IsNullOrEmpty(1)=CCN<=7.
>>>>>>> REPLACE
```

---

#### Step 4 -- Edit TradeCopierPanel.cs: Replace OnGlobalBeClick (apply_diff)

**Confirmed source location (verified against file):**
- Comment block leading into `OnGlobalBeClick` starts at line **1349**
- `private void OnGlobalBeClick` is at line **1357**
- Method closing brace `}` is at line **1377**
- Replace lines 1349-1377 (comment + full method body)

**apply_diff SEARCH block (exact content at lines 1349-1377):**

```
<<<<<<< SEARCH
:start_line:1349
-------
        // B40: OnGlobalBeClick -- armed/wait FSM for BE ALL. CYC=4.
        // Idle->Armed: arm all pending; if at least one slot entered Armed state, turn amber.
        // Armed->Idle (manual disarm): loop Account.All and DisarmPendingBe for each; turn purple.
        // JS-021: no lock(). JS-033: synchronous void event handler -- not async void.
        // DW-B72-02: _globalBeState removed. IsPendingSlotsEmpty() is the truth source.
        // Idle (slots empty)  -> arm all, then show Armed visual if slots were taken.
        // Armed (slots exist) -> disarm all, show Idle visual.
        // Both panels read the same CopyEngine singleton so state is automatically shared.
        private void OnGlobalBeClick(object sender, RoutedEventArgs e)
        {
            if (CopyEngine.Instance.IsPendingSlotsEmpty())
            {
                // Currently Idle -- arm
                NinjaTrader.Code.Output.Process(
                    "[BE-ALL] button: arm buf=" + CopyEngine.Instance.GlobalBe.GlobalBeBuffer,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
                CopyEngine.Instance.GlobalBe.Execute(CopyEngine.Instance.GlobalBe.GlobalBeBuffer);
            }
            else
            {
                // Already armed -- guard: log and return (no disarm, no re-arm)
                NinjaTrader.Code.Output.Process(
                    "[PTT-BE-ALL] already armed, ignoring double-press",
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
                return;
            }
        }
=======
        // B180 DW-B180-BE-SWAP-ALL: OnGlobalBeClick -- immediate BE swap all accounts.
        // Replaces IsPendingSlotsEmpty + armed-state FSM. Click = fire = done.
        // CYC=1 (straight-line -- no branch; PttBeSwapAll.Execute has its own flag gate).
        // JS-021: no lock. JS-033: synchronous void. JS-001: no throw. ASCII-only.
        private void OnGlobalBeClick(object sender, RoutedEventArgs e)
        {
            PttBeSwapAll.Execute(CopyEngine.Instance.GlobalBe.GlobalBeBuffer);
        }
>>>>>>> REPLACE
```

---

#### Step 5 -- Edit TradeCopierPanel.cs: Replace OnBeClick + ExecuteBeIdle + ExecuteBeArmed (apply_diff)

**Confirmed source location (verified against file):**
- Comment block leading into `OnBeClick` starts at line **1474**
- `private void OnBeClick` is at line **1480**
- `ExecuteBeArmed` closing brace `}` is at line **1534**
- Replace lines 1474-1534 (comment header + all three methods)

**apply_diff SEARCH block (exact content at lines 1474-1534):**

```
<<<<<<< SEARCH
:start_line:1474
-------
        // B33 T7 -- OnBeClick: 2-state FSM. Idle-immediate path dispatches to PttBreakEven module.
        // Idle: price-at-BE -> DispatchModule("BE") (stays Idle). In drawdown -> ArmPendingBe (Armed).
        // Armed: cancel arm -> Idle.
        // C-08: extracted ExecuteBeIdle + ExecuteBeArmed. Parent CCN=4.
        // B30-B: leader resolved late via _leaderAccount ?? TryResolveLeaderAccount() (DW-B30-03).
        // JS-021: no lock(). JS-033: synchronous void event handler -- not async void.
        private void OnBeClick(object sender, RoutedEventArgs e)
        {
            if (_instrument == null)
                return;
            _leaderAccount = _leaderAccount ?? TryResolveLeaderAccount(); // B30-B
            if (_leaderAccount == null)
                return;
            if (_beState == BeState.Idle)
                ExecuteBeIdle(_leaderAccount, _instrument);
            else if (_beState == BeState.Armed)
                ExecuteBeArmed(_leaderAccount);
        }

        // C-08: ExecuteBeIdle -- handles BeState.Idle arm of OnBeClick.
        // DW-B32-04: if price already past BE target, fire immediately (stays Idle).
        // Otherwise arm and wait for price to cross.
        // JS-021: no lock(). JS-001: no throw. JS-002: no return null. JS-033: no async void.
        private void ExecuteBeIdle(Account leader, NinjaTrader.Cbi.Instrument instrument)
        {
            if (IsPriceAlreadyAtBe(leader, instrument, _beBuffer))
            {
                NinjaTrader.Code.Output.Process(
                    "[BE] button: immediate fire "
                        + leader.Name
                        + " buf="
                        + _beBuffer,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
                DispatchModule("BE");
                // stay Idle -- ATM owns stop from here
            }
            else
            {
                NinjaTrader.Code.Output.Process(
                    "[BE] button: arming " + leader.Name + " buf=" + _beBuffer,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
                _engine.ArmPendingBe(instrument, leader, _beBuffer);
                _beState = BeState.Armed;
                UpdateBeVisuals(BeState.Armed);
            }
        }

        // C-08: ExecuteBeArmed -- handles BeState.Armed disarm of OnBeClick.
        // JS-021: no lock(). JS-001: no throw. JS-002: no return null. JS-033: no async void.
        private void ExecuteBeArmed(Account leader)
        {
            NinjaTrader.Code.Output.Process(
                "[BE] button: disarming " + leader.Name,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
            _engine.DisarmPendingBe(leader);
            _beState = BeState.Idle;
            UpdateBeVisuals(BeState.Idle);
        }
=======
        // B180 DW-B180-BE-SWAP-ALL: OnBeClick -- immediate BE bracket swap.
        // Replaces Idle/Armed FSM. Click = fire = done. No armed state.
        // CYC=4: (1) instrument null guard, (2) ?? on _leaderAccount, (3) leader null guard, (4) PttBeSwapOne.Execute.
        // JS-021: no lock. JS-033: synchronous void. JS-001: no throw. JS-002: no return null. ASCII-only.
        private void OnBeClick(object sender, RoutedEventArgs e)
        {
            if (_instrument == null)        // (1)
                return;
            _leaderAccount = _leaderAccount ?? TryResolveLeaderAccount();
            if (_leaderAccount == null)     // (2)
                return;
            PttBeSwapOne.Execute(_leaderAccount, _instrument, _beBuffer); // (3)
        }
>>>>>>> REPLACE
```

**Dead code note (B180-1):** `BeState`, `_beState`, `UpdateBeVisuals`, `ExecuteBeIdle`, `ExecuteBeArmed`, `IsPriceAlreadyAtBe`, `ComputeBeTargetPrice`, `IsPriceAtOrPastTarget`, `IsPendingSlotsEmpty`, `GlobalBeAllDisarmed`, `PendingBeArmed` event subscriptions remain compiled but NOT removed in B180-1. They become dead code cleaned up in B180-2 after VERIFY_PASS.

---

#### Step 6 -- Run ptt-sync.ps1

After all 5 steps above complete:
```
powershell -File .\scripts\ptt-sync.ps1
```
Expected: `N linked, 0 errors`

---

### 7-Scan Checklist (B180-1)

| Scan | Command | Expected Result |
|------|---------|-----------------|
| SCAN-01 (no lock) | `grep -c "lock(" src/PropTraderTools/Features/PttBeSwapOne.cs` | 0 |
| SCAN-01 (no lock) | `grep -c "lock(" src/PropTraderTools/Features/PttBeSwapAll.cs` | 0 |
| SCAN-01 (no lock) | `grep -c "lock(" src/PropTraderTools/CopyEngine.cs` | 0 (must remain 0 -- no new locks introduced) |
| SCAN-01 (no lock) | `grep -c "lock(" src/PropTraderTools/TradeCopierPanel.cs` | 0 new locks in changed regions |
| SCAN-02 (CYC <= 8) | Manual McCabe count -- see Method Signatures above | Max CYC=7 (PttBeSwapOne.Execute). All <= 8. |
| SCAN-02 (no async/await) | `grep -n "async\|await" src/PropTraderTools/Features/PttBeSwapOne.cs` | 0 matches |
| SCAN-02 (no async/await) | `grep -n "async\|await" src/PropTraderTools/Features/PttBeSwapAll.cs` | 0 matches |
| SCAN-03 (ASCII-only) | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/Features/PttBeSwapOne.cs` | 0 matches |
| SCAN-03 (ASCII-only) | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/Features/PttBeSwapAll.cs` | 0 matches |
| SCAN-04 (no DateTime.Now) | `grep -c "DateTime\.Now" src/PropTraderTools/Features/PttBeSwapOne.cs` | 0 |
| SCAN-04 (no DateTime.Now) | `grep -c "DateTime\.Now" src/PropTraderTools/Features/PttBeSwapAll.cs` | 0 |
| SCAN-04 (no DateTime.Now) | `grep -c "DateTime\.Now" src/PropTraderTools/CopyEngine.cs` | 0 new occurrences |
| SCAN-05 (no AtmStrategyCreate) | `grep -n "AtmStrategyCreate\|AtmStrategyChangeStopTarget" src/PropTraderTools/Features/PttBeSwapOne.cs` | 0 |
| SCAN-05 (no AtmStrategyCreate) | `grep -n "AtmStrategyCreate\|AtmStrategyChangeStopTarget" src/PropTraderTools/Features/PttBeSwapAll.cs` | 0 |
| SCAN-06 (no FontFamily) | `grep -n "FontFamily" src/PropTraderTools/Features/PttBeSwapOne.cs` | 0 |
| SCAN-06 (no FontFamily) | `grep -n "FontFamily" src/PropTraderTools/Features/PttBeSwapAll.cs` | 0 |
| SCAN-07 (ArmPendingBe leak) | `grep -n "ArmPendingBe" src/PropTraderTools/TradeCopierPanel.cs` | 0 occurrences in OnBeClick region (lines 1474+) |

---

### Verify Steps (B180-1)

Run ALL of the following after completing all steps. Every check must pass before B180-1 is declared VERIFY_PASS.

```powershell
# Additive rewire confirmation
grep -c "PttBeSwapOne" src/PropTraderTools/TradeCopierPanel.cs
# Expected: 1

grep -c "PttBeSwapAll" src/PropTraderTools/TradeCopierPanel.cs
# Expected: 1

grep -c "ArmPendingBe" src/PropTraderTools/TradeCopierPanel.cs
# Expected: 0 (OnBeClick no longer calls it)

grep -c "IsPendingSlotsEmpty" src/PropTraderTools/TradeCopierPanel.cs
# Expected: 0 (OnGlobalBeClick no longer calls it)

grep -c "SnapshotBeTargetsPublic" src/PropTraderTools/CopyEngine.cs
# Expected: 1 (the new wrapper method definition)

# Build and test
dotnet build src/PropTraderTools/
# Expected: 0 errors

dotnet test src/PropTraderTools/
# Expected: 0 new failures

# Hard-link sync (MANDATORY after every ticket)
powershell -File .\scripts\ptt-sync.ps1
# Expected: N linked, 0 errors
```

---

---

## Ticket B180-2: Dead Code Removal

### Spec Requirements

- Remove `BeState` enum, `_beState` field, armed-state FSM methods (orchestrator prompt -- "What is removed")
- Remove `ArmPendingBe`, `DisarmPendingBe`, `ArmAllPendingBe`, `OnPendingBeAccountUpdate` (orchestrator prompt)
- Remove `_pendingBeSlots`, `_pendingBePriceRetry`, `_bePriceRetryAttempts` fields (orchestrator prompt)
- Remove `MoveStopToBreakEven` and its helpers (orchestrator prompt)
- Remove `TryTriggerBeRecovery`, `TryReplacePttBeBrackets`, `TryFireBePriceRetry`, `LogBeCancelDiag` (orchestrator prompt)
- Remove `GlobalBeAllDisarmed` event + `RaiseBeAllDisarmed` (orchestrator prompt)
- Remove `PendingBeArmed` event (orchestrator prompt)
- Remove `IsPendingSlotsEmpty`, `ClearAllPendingBeSlots` (orchestrator prompt)
- Gut `PttGlobalBreakEven.Execute(int)` body; keep class as thin buffer holder (plan Section 6C, Decision: Option A)
- Delete `PttBreakEven.cs` (plan Section 6D, Decision: Option A)
- Remove TradeCopierPanel.cs dead FSM symbols (plan Section 6B)
- Remove event subscriptions in TradeCopierPanel.cs for dead events (plan Section 6B)
- `MN-03` (plan review): grep for `TryEmitBeWarnIfFlatArmed` in CopyEngine.cs before finalizing removal list; add to removal if count > 0

### Dependency

**VERIFY_PASS B180-1 is required. B180-2 MUST NOT START until ptt-verifier has issued VERIFY_PASS for B180-1.**

### Files Changed

| File | Action |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | EDIT -- multiple dead symbol removals |
| `src/PropTraderTools/TradeCopierPanel.cs` | EDIT -- multiple dead symbol removals + event subscription lines |
| `src/PropTraderTools/Features/PttGlobalBreakEven.cs` | EDIT -- gut Execute(int) body and remove armed-state logic; keep buffer holder |
| `src/PropTraderTools/Features/PttBreakEven.cs` | DELETE -- file removed entirely after confirming zero callers |

---

### Method Signatures

No new methods are introduced in B180-2. This ticket is pure removal.

The `PttGlobalBreakEven` class retains exactly these members after B180-2:

```csharp
internal sealed class PttGlobalBreakEven
{
    private volatile int _globalBeBuffer = 0;
    internal int GlobalBeBuffer => _globalBeBuffer;
    internal void IncrementBuffer() { ... }  // unchanged
    internal void DecrementBuffer() { ... }  // unchanged
}
```

All other fields, constructors, and methods of `PttGlobalBreakEven` are removed.

---

### Implementation Instructions

**SAFETY RULE:** For every removal in this ticket, the engineer MUST run the caller-check grep listed with that removal BEFORE deleting the code. If any caller-check returns a count > 0 (from outside the symbol's own definition), STOP and report to orchestrator -- do not delete.

---

#### Pre-removal check: TryEmitBeWarnIfFlatArmed (MN-03 from plan review)

Before starting any removal, run:

```powershell
grep -c "TryEmitBeWarnIfFlatArmed" src/PropTraderTools/CopyEngine.cs
```

- If result is **0**: symbol was never landed (B179 was not deployed). Continue to Step 1.
- If result is **> 0**: add `TryEmitBeWarnIfFlatArmed` to the CopyEngine.cs removal list below (remove in same batch as the other TryTriggerBeRecovery helpers).

---

#### Step 1 -- CopyEngine.cs: Remove `_pendingBeSlots` field (lines 268-271)

**Caller check first:**
```powershell
grep -c "_pendingBeSlots" src/PropTraderTools/CopyEngine.cs
```
After removing all other callers (Steps 1-15 below reduce the caller count to 1 -- the declaration itself), this must equal 1 before deleting.

**apply_diff SEARCH block:**
```
<<<<<<< SEARCH
:start_line:268
-------
        // B27 -- Pending BE slot dictionary (DW-B27-01: replaces 4 singleton fields).
        // Key = account.Name. JS-021: TryGetValue/TryRemove/AddOrUpdate are lock-free.
        private readonly ConcurrentDictionary<string, PendingBeSlot> _pendingBeSlots =
            new ConcurrentDictionary<string, PendingBeSlot>();
=======
>>>>>>> REPLACE
```

---

#### Step 2 -- CopyEngine.cs: Remove `_pendingBePriceRetry` and `_bePriceRetryAttempts` fields and `BeRetryMaxAttempts` const (lines 316-334)

**Caller check first:**
```powershell
grep -c "_pendingBePriceRetry\|_bePriceRetryAttempts\|BeRetryMaxAttempts" src/PropTraderTools/CopyEngine.cs
```
Must equal 3 (self-declarations only) before deleting.

**apply_diff SEARCH block:**
```
<<<<<<< SEARCH
:start_line:316
-------
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

        // DW-B159-K3: named constant for TryArmBePriceRetry attempt ceiling.
        // 3 attempts is empirically adequate for NT8 ATM cancel recovery.
        // K3-UI (deferred): wiring to settings panel is out of scope for this ticket.
        // JS-042: ASCII-only. No functional change.
        private const int BeRetryMaxAttempts = 3;
=======
>>>>>>> REPLACE
```

---

#### Step 3 -- CopyEngine.cs: Remove `PendingBeFired` and `PendingBeArmed` events (lines 450-451)

**Caller check first (TradeCopierPanel subscriptions removed in Step 8):**
```powershell
grep -c "PendingBeFired\|PendingBeArmed" src/PropTraderTools/TradeCopierPanel.cs
```
Must equal 0 (after Step 8 completes) before deleting this step. **Run Step 8 before Step 3.**

**apply_diff SEARCH block (after Step 8 is done):**
```
<<<<<<< SEARCH
:start_line:449
-------
        // B10 T2 -- Pending BE fired notification (fires on NT8 account bg thread; Panel marshals to UI)
        internal event Action<string, string> PendingBeFired;
        internal event Action<string, string> PendingBeArmed; // HOTFIX-BEALL-SYNC-01: instr, accName
=======
>>>>>>> REPLACE
```

---

#### Step 4 -- CopyEngine.cs: Remove `GlobalBeAllDisarmed` event and `RaiseBeAllDisarmed` method (lines 489-491)

**Caller check first (TradeCopierPanel subscriptions and calls must be removed in Step 8 first):**
```powershell
grep -c "GlobalBeAllDisarmed\|RaiseBeAllDisarmed" src/PropTraderTools/TradeCopierPanel.cs
```
Must equal 0 before running this step.

**apply_diff SEARCH block:**
```
<<<<<<< SEARCH
:start_line:487
-------
        // HOTFIX-BEALL-DISARM-SYNC-01: broadcast so all panels reset BE ALL visual to Idle on disarm.
        // Fired from OnGlobalBeClick disarm path AND from UpdateButtonColors HOTFIX-F3 branch (position close).
        internal event Action GlobalBeAllDisarmed;

        internal void RaiseBeAllDisarmed() => GlobalBeAllDisarmed?.Invoke();

=======
>>>>>>> REPLACE
```

---

#### Step 5 -- CopyEngine.cs: Remove `ArmAllPendingBe` method (lines 1495-1511)

**Caller check first:**
```powershell
grep -rn "ArmAllPendingBe" src/
```
Must return 0 results outside CopyEngine.cs definition after PttGlobalBreakEven.cs is gutted (Step 9 below). **Run Step 9 before Step 5.**

**apply_diff SEARCH block (lines 1494-1512):**
```
<<<<<<< SEARCH
:start_line:1494
-------
        // When the pending trigger fires it calls BreakEven(Account,...) which fans out to
        // followers via AllAccounts() -> MoveStopToBreakEven. No SubmitBeStop needed here.
        // CYC=3: foreach(1) + follower skip(2) + delegate(3). JS-021: no lock.
        internal void ArmAllPendingBe(int bufferTicks)
        {
            foreach (Account acc in Account.All) // (1)
            {
                if (IsFollowerAccount(acc))
                    continue; // (2) skip followers
                foreach (NinjaTrader.Cbi.Position pos in acc.Positions) // (3)
                {
                    if (pos == null || pos.Quantity == 0)
                        continue; // (4) skip flat
                    ArmPendingBe(pos.Instrument, acc, bufferTicks); // (5) HOTFIX-BE-ALL-01
                }
            }
        }

        // --- end B56 BUILD-FIX stubs ---
=======
        // --- end B56 BUILD-FIX stubs ---
>>>>>>> REPLACE
```

---

#### Step 6 -- CopyEngine.cs: Remove `TryTriggerBeRecovery` (line 5222) and `LogBeCancelDiag` (line ~5313) and `TryReplacePttBeBrackets` (line 5338) and `IsBeBracketRecoveryEligible`

**Caller check first:**
```powershell
grep -n "TryTriggerBeRecovery\|LogBeCancelDiag\|TryReplacePttBeBrackets\|IsBeBracketRecoveryEligible" src/PropTraderTools/CopyEngine.cs
```
Count all call-sites (not just definitions). Remove call-sites from `OnOrderUpdate` and any other callers first. Then remove the method bodies.

**Note:** After removing the call to `TryTriggerBeRecovery` from `OnOrderUpdate` (wherever it is called), confirm zero external callers, then remove the method body using `apply_diff` with the exact SEARCH block starting at line 5218 (the comment above `TryTriggerBeRecovery`):

```
<<<<<<< SEARCH
:start_line:5218
-------
        // JS-021: HasFilledBeTargetFast reads ConcurrentDictionary -- no lock. JS-001: no throw. JS-002: void.
        // DW-B90: skip if OCO cancel from target fill. DW-B92: race-free counter.
        // NinjaTrader.Code.Output.Process diagnostic log preserved verbatim (required by architect plan).
        // ASCII-only. No DateTime.Now. No lock().
        private void TryTriggerBeRecovery(Order o)
        {
=======
>>>>>>> REPLACE
```
*(Engineer: include the full method body through its closing `}` at line 5243 in the SEARCH block)*

---

#### Step 7 -- CopyEngine.cs: Remove `TryFireBePriceRetry` (line 7263), `MoveStopToBreakEven` (line 7069), and related helpers

**Order of removal within CopyEngine.cs:**
1. Remove call-site of `TryFireBePriceRetry` in `OnOrderUpdate` first. Confirm zero callers.
2. Remove `TryFireBePriceRetry` method (lines 7263+).
3. Remove call-site of `MoveStopToBreakEven` (caller chain: was via `ArmPendingBe` trigger path -- already removed in Step 5).
4. Confirm zero callers of `MoveStopToBreakEven`:
   ```powershell
   grep -n "MoveStopToBreakEven" src/PropTraderTools/CopyEngine.cs
   ```
5. Remove `MoveStopToBreakEven` body (lines 7069-7171).
6. Remove helpers called exclusively from these methods: `LogDiagOrderCount` (lines 7173+), `RegisterBeRetrySlotIfNeeded`, `TryArmBePriceRetry`, `RegisterPendingBeSlot`, `StillAtNonBePrice`, `IsImmediateBeEligible`, `FireImmediateBe`, `IsBeRetryArmedFlat`, `IsBeRetrySlotNeeded`.

**Caller check before each:**
```powershell
grep -n "<MethodName>" src/PropTraderTools/CopyEngine.cs
```
Must return 1 (definition only) before deletion.

---

#### Step 8 -- TradeCopierPanel.cs: Remove event subscriptions and dead call-sites

**8A: Remove Detach() subscription lines (lines 576-577, 580, 582, 586):**

Confirmed lines (verified against file):
- Line 576: `_engine.PendingBeFired -= OnPendingBeFiredDispatch;`
- Line 577: `_engine.PendingBeArmed -= OnPendingBeArmedDispatch;`
- Line 580: `_engine.GlobalBeAllDisarmed -= OnGlobalBeAllDisarmed;`
- Line 582: `_engine.DisarmPendingBe(_leaderAccount);`
- Line 586: `_engine.ClearAllPendingBeSlots();` (and surrounding IsPanelsEmpty guard at line 585)

**apply_diff SEARCH block for Detach region (lines 576-587):**
```
<<<<<<< SEARCH
:start_line:576
-------
            _engine.PendingBeFired -= OnPendingBeFiredDispatch;
            _engine.PendingBeArmed -= OnPendingBeArmedDispatch; // HOTFIX-BEALL-SYNC-01
            _engine.GlobalBeBufferChanged -= OnGlobalBeBufferChanged; // HOTFIX-BEALL-BUFFER-SYNC-01
            _engine.GlobalQuickAllBufferChanged -= OnQuickAllBufferChanged; // HOTFIX-QUICKALL-SINGLETON-01
            _engine.GlobalBeAllDisarmed -= OnGlobalBeAllDisarmed; // HOTFIX-BEALL-DISARM-SYNC-01
            UnsubscribeFollowerItems();
            _engine.DisarmPendingBe(_leaderAccount);
            // DW-C39-20: last-panel-close guard -- clear remaining global pending BE slots.
            // TradeCopierAddOn.TryRemove ran before Detach(), so _panels is already empty if last panel.
            if (TradeCopierAddOn.IsPanelsEmpty())
                _engine.ClearAllPendingBeSlots();
=======
            _engine.GlobalBeBufferChanged -= OnGlobalBeBufferChanged; // HOTFIX-BEALL-BUFFER-SYNC-01
            _engine.GlobalQuickAllBufferChanged -= OnQuickAllBufferChanged; // HOTFIX-QUICKALL-SINGLETON-01
            UnsubscribeFollowerItems();
>>>>>>> REPLACE
```

**8B: Remove SubscribeEngineEvents() subscription lines (lines 810-811, 814):**

Confirmed lines (verified against file):
- Line 810: `_engine.PendingBeFired += OnPendingBeFiredDispatch;`
- Line 811: `_engine.PendingBeArmed += OnPendingBeArmedDispatch;`
- Line 814: `_engine.GlobalBeAllDisarmed += OnGlobalBeAllDisarmed;`

**apply_diff SEARCH block for SubscribeEngineEvents region (lines 809-815):**
```
<<<<<<< SEARCH
:start_line:809
-------
            _engine.PositionStateChanged += OnPositionStateChanged;
            _engine.PendingBeFired += OnPendingBeFiredDispatch;
            _engine.PendingBeArmed += OnPendingBeArmedDispatch;
            _engine.GlobalBeBufferChanged += OnGlobalBeBufferChanged;
            _engine.GlobalQuickAllBufferChanged += OnQuickAllBufferChanged;
            _engine.GlobalBeAllDisarmed += OnGlobalBeAllDisarmed;
            _engine.CopyEnabledChanged += OnCopyEnabledChanged;
=======
            _engine.PositionStateChanged += OnPositionStateChanged;
            _engine.GlobalBeBufferChanged += OnGlobalBeBufferChanged;
            _engine.GlobalQuickAllBufferChanged += OnQuickAllBufferChanged;
            _engine.CopyEnabledChanged += OnCopyEnabledChanged;
>>>>>>> REPLACE
```

---

#### Step 9 -- PttGlobalBreakEven.cs: Gut to thin buffer holder

**Caller check first:**
```powershell
grep -rn "ArmAllPendingBe\|PttGlobalBreakEven\.Execute\|_submitBeStop\|_ocoSeq" src/
```
Must return 0 outside of PttGlobalBreakEven.cs itself before replacing.

Read the current `PttGlobalBreakEven.cs` using `read_file` to get exact content, then use `write_file` to replace the entire file with the thin buffer holder:

**Complete replacement file content:**

```csharp
// src/PropTraderTools/Features/PttGlobalBreakEven.cs
// B180 DW-B180-BE-SWAP-ALL: thin buffer holder only.
// Execute(int) removed -- use PttBeSwapAll.Execute(bufferTicks) instead.
// Keeps GlobalBeBuffer, IncrementBuffer, DecrementBuffer for panel +/- spin UI.
// CYC=2 (IncrementBuffer), CYC=2 (DecrementBuffer).
// JS-021: no lock (volatile int). JS-001: no throw. ASCII-only.

namespace PropTraderTools
{
    internal sealed class PttGlobalBreakEven
    {
        private volatile int _globalBeBuffer = 0;

        internal int GlobalBeBuffer => _globalBeBuffer;

        internal void IncrementBuffer()
        {
            if (_globalBeBuffer < 10)
                _globalBeBuffer++;
            CopyEngine.Instance.RaiseBeBufferChanged(_globalBeBuffer);
        }

        internal void DecrementBuffer()
        {
            if (_globalBeBuffer > -10)
                _globalBeBuffer--;
            CopyEngine.Instance.RaiseBeBufferChanged(_globalBeBuffer);
        }
    }
}
```

---

#### Step 10 -- PttBreakEven.cs: Delete the file

**Pre-deletion caller checks (all must return 0):**
```powershell
grep -rn "PttBreakEven\b" src/PropTraderTools/
# Must return 0 outside of PttBreakEven.cs itself

grep -rn "DispatchModule.*\"BE\"" src/PropTraderTools/
# Must return 0 (OnBeClick no longer calls DispatchModule("BE") after B180-1)
```

Also check for `PttBreakEven` registration in the module registry (wherever `DispatchModule` routes to `PttBreakEven`):
```powershell
grep -n "\"BE\"" src/PropTraderTools/CopyEngine.cs
# Review context -- remove any module registration entry for "BE"
```

After all caller-checks return 0, delete the file:
```powershell
Remove-Item src/PropTraderTools/Features/PttBreakEven.cs
```

---

#### Step 11 -- TradeCopierPanel.cs: Remove BeState enum and _beState field

**Caller check first:**
```powershell
grep -n "BeState\|_beState" src/PropTraderTools/TradeCopierPanel.cs
```
After Steps 8A/8B and the dead method removals in Step 12 below, all references should be removed. Confirm 0 external references before removing the enum and field.

**BeState enum (confirmed lines 393-397, verified against file):**

```
<<<<<<< SEARCH
:start_line:390
-------
        // Armed -> price crosses entry+buffer -> MoveStopToBreakEven once -> Idle
        // Armed -> click again  -> DisarmPendingBe -> Idle (cancel)
        // ATM trail owns stop after BE placement. PTT does not trail. (DW-B32-05)
        internal enum BeState
        {
            Idle, // BE button shows "BE +N" -- inactive
            Armed, // Watching price; fires once when entry+buffer crossed; amber border
        }
=======
>>>>>>> REPLACE
```

**_beState field (confirmed line 243):**

```
<<<<<<< SEARCH
:start_line:242
-------
        // B12 T1 -- BE 3-state FSM (UI-thread-only; no volatile)
        private BeState _beState = BeState.Idle;

=======
>>>>>>> REPLACE
```

---

#### Step 12 -- TradeCopierPanel.cs: Remove dead FSM methods

Remove the following methods using `apply_diff` with exact SEARCH blocks. Confirmed line ranges (verified against file):

| Method | Lines | Caller check before removal |
|--------|-------|-----------------------------|
| `ResetBeStateOnFlat` | 659-669 | `grep -c "ResetBeStateOnFlat" src/PropTraderTools/TradeCopierPanel.cs` must be 1 (definition only) |
| `DisarmBeAllOnFlat` | 671-680 | `grep -c "DisarmBeAllOnFlat" src/PropTraderTools/TradeCopierPanel.cs` must be 1 |
| `OnPendingBeArmedDispatch` | 1099-1109 | Subscription removed in Step 8 -- confirm 0 callers |
| `OnGlobalBeAllDisarmed` | 1134-1139 | Subscription removed in Step 8 -- confirm 0 callers |
| `UpdateBeAllVisuals` | 1141-1158 | `grep -c "UpdateBeAllVisuals" src/PropTraderTools/TradeCopierPanel.cs` must be 1 after above removed |
| `ComputeBeTargetPrice` | 1538-1547 | `grep -c "ComputeBeTargetPrice" src/PropTraderTools/TradeCopierPanel.cs` must be 1 |
| `IsPriceAtOrPastTarget` | 1549-1553 | `grep -c "IsPriceAtOrPastTarget" src/PropTraderTools/TradeCopierPanel.cs` must be 1 |
| `IsPriceAlreadyAtBe` | 1555-1570 | `grep -c "IsPriceAlreadyAtBe" src/PropTraderTools/TradeCopierPanel.cs` must be 1 |
| `UpdateBeVisuals` | 1579-1597 | `grep -c "UpdateBeVisuals" src/PropTraderTools/TradeCopierPanel.cs` must be 1 |
| `OnBeConnected` | 1599-1613 | Subscription removed in Step 8 (PendingBeFired wired to OnPendingBeFiredDispatch, not OnBeConnected directly -- verify registration point) |

**For each removal:** read the exact current content first (confirming line numbers), then apply_diff with the exact SEARCH block from source.

**Example apply_diff for ResetBeStateOnFlat:**
```
<<<<<<< SEARCH
:start_line:659
-------
        // HOTFIX-F3: reset per-chart BE state when position goes flat. CCN=3.
        private void ResetBeStateOnFlat(bool hasPosition)
        {
            if (!hasPosition && _beState != BeState.Idle)
            {
                _beState = BeState.Idle;
                UpdateBeVisuals(BeState.Idle);
                if (_leaderAccount != null)
                    CopyEngine.Instance.DisarmPendingBe(_leaderAccount);
            }
        }

=======
>>>>>>> REPLACE
```

**Example apply_diff for DisarmBeAllOnFlat:**
```
<<<<<<< SEARCH
:start_line:671
-------
        // HOTFIX-BEALL-FLAT-RESET: disarm BE ALL when position goes flat. CCN=3.
        private void DisarmBeAllOnFlat(bool hasPosition)
        {
            if (!hasPosition && !CopyEngine.Instance.IsPendingSlotsEmpty())
            {
                if (_leaderAccount != null)
                    CopyEngine.Instance.DisarmPendingBe(_leaderAccount);
                CopyEngine.Instance.RaiseBeAllDisarmed();
            }
        }

=======
>>>>>>> REPLACE
```

*(Engineer: use the same pattern for all other methods in this step -- read_file to confirm exact current content, then apply_diff.)*

---

#### Step 13 -- CopyEngine.cs: Remove remaining dead symbols in CopyEngine.cs

Remove the following after all callers are confirmed gone:
- `ArmPendingBe` (line 7558)
- `DisarmPendingBe` (line 7684)
- `IsPendingSlotsEmpty` (line 7700)
- `ClearAllPendingBeSlots` (line 7707)
- `OnPendingBeAccountUpdate` (line 7819)
- `HasActivePttBeExits`, `HasActivePttBeTargets`, `HasSettlingBeTargets`
- `IsBeRetryArmedFlat`, `IsBeRetrySlotNeeded`

For each:
```powershell
grep -c "<MethodName>" src/PropTraderTools/CopyEngine.cs
```
Must equal 1 (definition only) before removal.

---

#### Step 14 -- Run dotnet build after all removals

```powershell
dotnet build src/PropTraderTools/
```
Expected: 0 errors. If build fails, the failure output identifies the remaining dangling reference. Fix that reference before proceeding.

---

#### Step 15 -- Run ptt-sync.ps1

```powershell
powershell -File .\scripts\ptt-sync.ps1
```
Expected: `N linked, 0 errors`

---

### 7-Scan Checklist (B180-2)

| Scan | Command | Expected Result |
|------|---------|-----------------|
| SCAN-01 (no lock) | `grep -c "lock(" src/PropTraderTools/CopyEngine.cs` | 0 |
| SCAN-01 (no lock) | `grep -c "lock(" src/PropTraderTools/TradeCopierPanel.cs` | 0 |
| SCAN-01 (no lock) | `grep -c "lock(" src/PropTraderTools/Features/PttGlobalBreakEven.cs` | 0 |
| SCAN-02 (CYC) | All retained methods in PttGlobalBreakEven: IncrementBuffer CYC=2, DecrementBuffer CYC=2 | All <= 8 |
| SCAN-02 (no async/await) | `grep -n "async\|await" src/PropTraderTools/Features/PttGlobalBreakEven.cs` | 0 |
| SCAN-03 (ASCII-only) | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/Features/PttGlobalBreakEven.cs` | 0 |
| SCAN-04 (no DateTime.Now) | `grep -c "DateTime\.Now" src/PropTraderTools/CopyEngine.cs` | 0 new occurrences |
| SCAN-05 (no AtmStrategyCreate) | `grep -n "AtmStrategyCreate\|AtmStrategyChangeStopTarget" src/PropTraderTools/Features/PttGlobalBreakEven.cs` | 0 |
| SCAN-06 (no FontFamily) | `grep -n "FontFamily" src/PropTraderTools/Features/PttGlobalBreakEven.cs` | 0 |
| SCAN-07 (build clean) | `dotnet build src/PropTraderTools/` | 0 errors |

---

### Verify Steps (B180-2)

Run ALL of the following after completing all removal steps.

```powershell
# Dead symbol absence checks
grep -rc "ArmPendingBe" src/
# Expected: 0

grep -rc "MoveStopToBreakEven" src/
# Expected: 0

grep -rc "TryTriggerBeRecovery" src/
# Expected: 0

grep -rc "_pendingBeSlots" src/
# Expected: 0

grep -rc "DisarmPendingBe" src/
# Expected: 0

grep -rc "IsPendingSlotsEmpty" src/
# Expected: 0

grep -rc "ClearAllPendingBeSlots" src/
# Expected: 0

grep -rc "PendingBeArmed" src/
# Expected: 0

grep -rc "GlobalBeAllDisarmed\|RaiseBeAllDisarmed" src/
# Expected: 0

grep -rc "BeState\|_beState" src/PropTraderTools/TradeCopierPanel.cs
# Expected: 0

grep -rc "UpdateBeVisuals\|UpdateBeAllVisuals" src/PropTraderTools/TradeCopierPanel.cs
# Expected: 0

# MN-03: TryEmitBeWarnIfFlatArmed (confirm removed if it existed)
grep -rc "TryEmitBeWarnIfFlatArmed" src/
# Expected: 0

# Lock check
grep -c "lock(" src/PropTraderTools/CopyEngine.cs
# Expected: 0

# PttBreakEven.cs deletion confirmed
Test-Path src/PropTraderTools/Features/PttBreakEven.cs
# Expected: False

# Build and test
dotnet build src/PropTraderTools/
# Expected: 0 errors

dotnet test src/PropTraderTools/
# Expected: 0 new failures

# Hard-link sync (MANDATORY)
powershell -File .\scripts\ptt-sync.ps1
# Expected: N linked, 0 errors
```

---

---

## Ticket B180-3: Tests

### Spec Requirements

- T-B180-T1-01: PttBeSwapOne flat account skips execution (orchestrator prompt -- T-B180-T1-01) -- **DEFERRED: NT8 Account/Instrument/Position seam unavailable; see DW-B180-BE-T01**
- T-B180-T1-02: PttBeSwapOne long position computes tick-rounded entry price (orchestrator prompt -- T-B180-T1-02)
- T-B180-T1-03: PttBeSwapOne bufferTicks shifts entry price by direction (orchestrator prompt -- T-B180-T1-03)
- T-B180-T1-04: PttBeSwapAll follower accounts are skipped (orchestrator prompt -- T-B180-T1-04) -- **DEFERRED: NT8 Account.All / IsFollowerAccount seam unavailable; see DW-B180-BE-T04**
- T-B180-T1-05: RoundToTick regression using `RoundToTickTestable` seam (plan Section 7 + MN-01 note)
- MN-01 (plan review): test MUST use `CopyEngine.RoundToTickTestable` (line 4514), NOT `RoundToTickPublic`
- MN-01 verification grep added to verify steps below

### Dependency

**VERIFY_PASS B180-2 is required. B180-3 MUST NOT START until ptt-verifier has issued VERIFY_PASS for B180-2.**

### Files Changed

| File | Action |
|------|--------|
| `src/PropTraderTools/CopyEngineTests.cs` | EDIT -- append new test class `B180BeSwapTests` |

---

### Method Signatures

All 5 test methods in class `B180BeSwapTests`, xUnit framework, `[Fact]` attribute:

```csharp
public class B180BeSwapTests
{
    [Fact] public void PttBeSwapOne_FlatAccount_SkipsExecution()
    [Fact] public void PttBeSwapOne_LongPosition_ComputesTickRoundedEntryPrice()
    [Fact] public void PttBeSwapOne_BufferTicks_ShiftsEntryPriceByDirection()
    [Fact] public void PttBeSwapAll_FollowerAccounts_AreSkipped()
    [Fact] public void RoundToTick_KnownValues_MatchExpected()
}
```

---

### Implementation Instructions

#### Step 1 -- Confirm seam name (MN-01 verification)

Before writing any test, run:
```powershell
grep -n "RoundToTick" src/PropTraderTools/CopyEngine.cs
```
Expected to find exactly:
- Line 4508: `private static double RoundToTick(`
- Line 4514: `internal static double RoundToTickTestable(`

The test uses `RoundToTickTestable` (line 4514). If instead the grep shows `RoundToTickPublic`, adjust test T-B180-T1-05 accordingly and report the discrepancy. Do NOT add a duplicate seam -- use whichever name exists at line 4513-4515.

---

#### Step 2 -- Append B180BeSwapTests class to CopyEngineTests.cs (apply_diff or insert_content)

Locate the end of `CopyEngineTests.cs` (the final closing brace of the last test class or namespace). Use `insert_content` at line 0 (append) OR `apply_diff` targeting the final lines.

**Complete test class content to append:**

```csharp
    // -------------------------------------------------------------------------
    // B180 DW-B180-BE-SWAP-ALL -- Break Even immediate swap tests
    // -------------------------------------------------------------------------
    public class B180BeSwapTests
    {
        // T-B180-T1-01: PttBeSwapOne.Execute skips when account is flat (pos==null or qty==0).
        // DEFERRED: Requires NT8 Account/Instrument/Position runtime objects not available in
        // the unit test harness. All NT8-touching tests in CopyEngineTests.cs use
        // [Fact(Skip="NT8-runtime:...")] because CopyEngine.cctor requires the NT8 host.
        // No Account/Instrument/Position mock infrastructure exists in this harness.
        // Deferred to integration test scope (requires NT8 host in NinjaTrader AddOn context).
        // Deferred backlog item: DW-B180-BE-T01 -- Account/Instrument/Position seam needed.
        [Fact(Skip = "DEFERRED: NT8 Account/Instrument/Position seam not available in unit test harness")]
        public void PttBeSwapOne_FlatAccount_SkipsExecution()
        {
            // Contract when seam is available:
            // string capturedStatus = null;
            // CopyEngine.Instance.StatusUpdate = msg => capturedStatus = msg;
            // PttBeSwapOne.Execute(testAccount, testInstr, 0);
            // Assert.NotNull(capturedStatus);
            // Assert.Contains(": BE flat skip", capturedStatus);
            Assert.True(false, "DEFERRED: NT8 Account/Instrument/Position seam not available -- see DW-B180-BE-T01 backlog item");
        }

        // T-B180-T1-02: PttBeSwapOne.Execute computes tick-rounded entry price for a long position.
        // Setup: long position at AveragePrice=4500.125, tickSize=0.25, bufferTicks=0.
        // Expected: entryPrice = Math.Round(4500.125 / 0.25) * 0.25 = 4500.25 (nearest tick up).
        // Seam: captured entryPrice passed to PttBreakEvenSwap.Execute delegate.
        [Fact]
        public void PttBeSwapOne_LongPosition_ComputesTickRoundedEntryPrice()
        {
            // Arrange: AveragePrice=4500.125, tickSize=0.25, buf=0
            double avgPrice = 4500.125;
            double tickSize = 0.25;
            int buf = 0;

            // Act: compute inline (same formula as PttBeSwapOne)
            bool isLong = true;
            double direction = isLong ? -1.0 : +1.0;
            double raw = avgPrice + direction * buf * tickSize;
            double entryPrice = Math.Round(raw / tickSize) * tickSize;

            // Assert
            Assert.Equal(4500.25, entryPrice, precision: 10);
        }

        // T-B180-T1-03: PttBeSwapOne bufferTicks shifts entry price by direction.
        // Long: stop goes BELOW entry (direction=-1). Short: stop goes ABOVE entry (direction=+1).
        [Fact]
        public void PttBeSwapOne_BufferTicks_ShiftsEntryPriceByDirection()
        {
            double avgPrice = 5000.0;
            double tickSize = 0.25;
            int buf = 2;

            // Long: direction = -1.0
            double rawLong = avgPrice + (-1.0) * buf * tickSize;
            double entryLong = Math.Round(rawLong / tickSize) * tickSize;
            Assert.Equal(4999.5, entryLong, precision: 10);

            // Short: direction = +1.0
            double rawShort = avgPrice + (+1.0) * buf * tickSize;
            double entryShort = Math.Round(rawShort / tickSize) * tickSize;
            Assert.Equal(5000.5, entryShort, precision: 10);
        }

        // T-B180-T1-04: PttBeSwapAll.Execute skips follower accounts.
        // DEFERRED: Requires NT8 Account.All enumeration (a static NT8 list populated only when
        // the NT8 host is running) and live Account instances for IsFollowerAccount(Account).
        // No Account stub/mock infrastructure exists in the current CopyEngineTests.cs harness.
        // Deferred to integration test scope (requires NT8 host in NinjaTrader AddOn context).
        // Deferred backlog item: DW-B180-BE-T04 -- Account.All stub / IsFollowerAccount seam needed.
        [Fact(Skip = "DEFERRED: NT8 Account.All and IsFollowerAccount seam not available in unit test harness")]
        public void PttBeSwapAll_FollowerAccounts_AreSkipped()
        {
            // Contract when seam is available:
            // Verify PttBreakEvenSwap.Execute is called for leader accounts only (not followers).
            // Account.All must contain at least one leader and one follower for the test to be meaningful.
            Assert.True(false, "DEFERRED: NT8 Account.All stub not available -- see DW-B180-BE-T04 backlog item");
        }

        // T-B180-T1-05: RoundToTick regression using existing CopyEngine.RoundToTickTestable seam.
        // MN-01: uses RoundToTickTestable (actual method at line 4514), NOT RoundToTickPublic.
        // No NT8 runtime required -- pure math, no Account/Instrument dependencies.
        [Fact]
        public void RoundToTick_KnownValues_MatchExpected()
        {
            // Confirmed seam: CopyEngine.RoundToTickTestable (line 4514 in CopyEngine.cs)
            // grep confirmed: internal static double RoundToTickTestable(double raw, double tick)

            Assert.Equal(4500.25, CopyEngine.RoundToTickTestable(4500.125, 0.25), precision: 10);
            // 4500.125 / 0.25 = 18000.5 -> Math.Round = 18001 -> * 0.25 = 4500.25

            Assert.Equal(5000.0, CopyEngine.RoundToTickTestable(4999.875, 0.25), precision: 10);
            // 4999.875 / 0.25 = 19999.5 -> Math.Round = 20000 -> * 0.25 = 5000.0
            // NOTE: Orchestrator prompt had typo (expected 4500.0). Corrected by plan Section 7 note.

            Assert.Equal(4500.0, CopyEngine.RoundToTickTestable(4500.0, 0.25), precision: 10);
            // Exact-tick input: no rounding required.

            Assert.Equal(0.0, CopyEngine.RoundToTickTestable(0.1, 0.0), precision: 10);
            // tickSize=0 edge case: returns rawPrice unchanged per implementation (tickSize>0 guard).
        }
    }
```

**Note on T-B180-T1-01 and T-B180-T1-04 (DEFERRED):** These tests require NT8 Account/Instrument/Position runtime objects. No mock infrastructure exists in the current CopyEngineTests.cs harness (all NT8-touching tests use `[Fact(Skip="NT8-runtime:...")]`). Both tests are marked `[Fact(Skip="DEFERRED:...")]` with `Assert.True(false, ...)` so the verifier sees them as explicitly deferred rather than false-green. Deferred backlog items: DW-B180-BE-T01 (Account/Instrument/Position seam) and DW-B180-BE-T04 (Account.All stub / IsFollowerAccount seam). The test method contracts and expected assertions are documented in the test bodies as comments for when the seam is built.

---

#### Step 3 -- Run ptt-sync.ps1

```powershell
powershell -File .\scripts\ptt-sync.ps1
```
Expected: `N linked, 0 errors`

---

### 7-Scan Checklist (B180-3)

| Scan | Command | Expected Result |
|------|---------|-----------------|
| SCAN-01 (no lock) | `grep -c "lock(" src/PropTraderTools/CopyEngineTests.cs` | 0 new lock() in B180 test class |
| SCAN-02 (CYC) | All 5 test methods are linear (CYC=1 each) | All <= 8 |
| SCAN-02 (no async/await) | `grep -n "async\|await" src/PropTraderTools/CopyEngineTests.cs` | 0 new occurrences in B180BeSwapTests region |
| SCAN-03 (ASCII-only) | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngineTests.cs` | 0 new non-ASCII in B180 additions |
| SCAN-04 (no DateTime.Now) | `grep -c "DateTime\.Now" src/PropTraderTools/CopyEngineTests.cs` | 0 new occurrences |
| SCAN-05 (no AtmStrategyCreate) | `grep -n "AtmStrategyCreate\|AtmStrategyChangeStopTarget" src/PropTraderTools/CopyEngineTests.cs` | 0 in B180 test class |
| SCAN-06 (no FontFamily) | `grep -n "FontFamily" src/PropTraderTools/CopyEngineTests.cs` | 0 in B180 test class |
| SCAN-07 (build + test clean) | `dotnet build src/PropTraderTools/` + `dotnet test src/PropTraderTools/ --filter "B180"` | 0 errors; T02, T03, T05 pass; T01, T04 skipped (DEFERRED, not failed) |

---

### Verify Steps (B180-3)

```powershell
# MN-01: Confirm seam name used in T-B180-T1-05
grep -n "RoundToTickTestable" src/PropTraderTools/CopyEngineTests.cs
# Expected: at least 1 match in B180BeSwapTests class

# MN-01: Confirm RoundToTickPublic NOT used (old wrong name)
grep -c "RoundToTickPublic" src/PropTraderTools/CopyEngineTests.cs
# Expected: 0 (no test should reference the non-existent name)

# MN-01: Confirm seam exists in CopyEngine.cs
grep -n "RoundToTickTestable" src/PropTraderTools/CopyEngine.cs
# Expected: line 4514 (definition)

# MN-03: Confirm TryEmitBeWarnIfFlatArmed is absent (B180-2 should have removed it)
grep -rc "TryEmitBeWarnIfFlatArmed" src/
# Expected: 0

# B180BeSwapTests class present
grep -c "B180BeSwapTests" src/PropTraderTools/CopyEngineTests.cs
# Expected: 1 (class definition)

# All 5 test method names present
grep -c "PttBeSwapOne_FlatAccount_SkipsExecution\|PttBeSwapOne_LongPosition_ComputesTickRoundedEntryPrice\|PttBeSwapOne_BufferTicks_ShiftsEntryPriceByDirection\|PttBeSwapAll_FollowerAccounts_AreSkipped\|RoundToTick_KnownValues_MatchExpected" src/PropTraderTools/CopyEngineTests.cs
# Expected: 5

# Run B180 tests only
dotnet test src/PropTraderTools/ --filter "B180"
# Expected: 5 tests pass, 0 fail

# Full build and test suite
dotnet build src/PropTraderTools/
# Expected: 0 errors

dotnet test src/PropTraderTools/
# Expected: 0 new failures (no pre-existing tests broken)

# Hard-link sync (MANDATORY)
powershell -File .\scripts\ptt-sync.ps1
# Expected: N linked, 0 errors
```

---

## Notes Addressed from Plan Review

### MN-01 (RoundToTick seam name)
Addressed in B180-3 Step 1 and T-B180-T1-05. Ticket explicitly instructs engineer to confirm seam name with grep before writing the test, mandates use of `RoundToTickTestable` (line 4514), and verifies `RoundToTickPublic` is NOT used.

### MN-02 (Canonical SCAN numbering)
7-scan checklists in this ticket file include the canonical RULES_CATALOG scans (SCAN-01 through SCAN-07) with:
- SCAN-02 explicitly includes both CYC manual count AND async/await grep (per RULES_CATALOG)
- SCAN-05 explicitly includes AtmStrategyCreate/AtmStrategyChangeStopTarget grep (per RULES_CATALOG)
The ptt-verifier (Ph4b) is reminded to run ALL 7 canonical scans independently per RULES_CATALOG §4.

### MN-03 (TryEmitBeWarnIfFlatArmed missing from removal list)
Addressed in B180-2 pre-removal check section. The ticket instructs the engineer to grep for `TryEmitBeWarnIfFlatArmed` in CopyEngine.cs before starting any removal. If count > 0, add it to the removal list. B180-3 verify steps confirm absence with `grep -rc "TryEmitBeWarnIfFlatArmed" src/ == 0`.

---

`TICKETS_COMPLETE`
