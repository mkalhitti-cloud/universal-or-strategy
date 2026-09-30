# B180 Architecture Plan
# Block: B180 -- DW-B180-BE-SWAP-ALL
# Produced by: ptt-architect (Phase 1)
# Source inputs: B180-orchestrator-prompt.md, B177/06-deferred-backlog.md
# Source files read: TradeCopierPanel.cs, CopyEngine.cs, PttBreakEvenSwap.cs,
#                    PttGlobalBreakEven.cs, PttBreakEven.cs
# Status: AWAITING REVIEW (Phase 2)

---

## Section 0: LANE-SPLIT GATE

**Q1: Does this block introduce concurrent work streams that must be isolated?**
No. All 3 tickets are strictly sequential: B180-1 (additive) -> B180-2 (removal) -> B180-3 (tests). No parallel tracks.

**Q2: Does any ticket depend on another ticket in a way that prevents merging?**
Yes. B180-2 (removal) is explicitly gated on B180-1 VERIFY_PASS. The dead code removed in B180-2 becomes dead only after B180-1 rewires the handlers. Combining would produce a broken intermediate state.

**Q3: Are there more than 3 files touched that span independent domains?**
No. The files touched are: new PttBeSwapOne.cs, new PttBeSwapAll.cs, existing CopyEngine.cs (wrapper + removal), existing TradeCopierPanel.cs (handler rewire + dead code), existing PttGlobalBreakEven.cs (gut to thin buffer holder), delete PttBreakEven.cs. All within the BE domain.

**Q4: Does the work fit the 3-ticket staged model specified by the orchestrator?**
Yes. The orchestrator explicitly mandates 3 tickets: B180-1 (additive), B180-2 (dead code removal), B180-3 (tests).

**LANE-SPLIT GATE RESULT: SINGLE-PIPELINE** -- 3 tickets, strictly sequential, no parallel lanes.

---

## Section 1: Mission Summary

Block B180 replaces the deferred-wait Break Even (BE) and Break Even ALL (BE ALL) buttons with an immediate synchronous bracket-swap design. The old flow armed a price watcher (`_pendingBeSlots`, `BeState` FSM) and waited for price to return to entry before moving the stop -- creating silent no-ops on early flatten (DW-B179), stale slot ghost-fires, and approximately 40 methods of race-management complexity in CopyEngine.cs. The new flow is: button click -> cancel existing brackets -> submit new PTT-BE-Stop at entry price (ticked, +/- buffer) -> done. No armed state. No slots. No subscriptions. Entry price uses `pos.AveragePrice` rounded to tick via the same `Math.Round(raw / tickSize) * tickSize` formula as the existing `MoveStopToBreakEven` method. The `PttBreakEvenSwap.Execute` engine (already immediate and correct) is reused as-is. The +/- spin controls and buffer state (`GlobalBe.GlobalBeBuffer`) are preserved unchanged. The only change visible to the user is that the button never turns amber (no Armed visual state). This closes DW-B179-BE-ALL-SILENT-CANCEL at root: the race cannot occur if there is no armed state to race against.

---

## Section 2: Staged Ticket Rationale

**Why B180-1 (additive) must ship and verify BEFORE B180-2 (removal):**

The orchestrator prompt includes an explicit SCOPE GATE:
> "The pipeline MUST work in stages: B180-1 rewires the buttons (additive only -- new click handlers), B180-2 removes dead code. The architect MUST NOT combine both into one ticket. Additive change ships and verifies first; removal ships and verifies second."

The technical reason is correctness-by-construction: the dead code in B180-2 is only truly dead after B180-1's rewire is verified. If the removal lands before the additive rewire is verified working, there is no fallback path -- the build would break or the buttons would silently no-op with no replacement path in place.

The staged approach provides a safe checkpoint:
1. B180-1 lands: new handlers fire correctly, old handlers/FSM are dead but still compile. Button works. No regressions.
2. B180-1 VERIFY_PASS confirmed (by ptt-verifier). ONLY THEN:
3. B180-2 lands: dead code removed. Build stays clean because all callers were already removed in B180-1.

This is the "make illegal states unrepresentable" principle applied to the pipeline itself: the removal literally cannot break anything after the additive ticket is verified clean.

---

## Section 3: New Files

### 3A: `src/PropTraderTools/Features/PttBeSwapOne.cs` (NEW)

```csharp
// src/PropTraderTools/Features/PttBeSwapOne.cs
// B180 DW-B180-BE-SWAP-ALL: per-chart immediate BE bracket swap.
// Replaces PttBreakEven module (DispatchModule("BE") path).
// Pattern: mirrors PttBeSwapAll.ExecuteOne but scoped to one account.
// CYC=5: (1) flag-gate, (2) instr null, (3) tickSize guard, (4) flat guard, (5) PttBreakEvenSwap.
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

**Verification against RULES_CATALOG:**
- ASCII-only: All string literals are straight ASCII. "[BE] swap:", "Break Even requires Pro tier", ": BE flat skip" -- no Unicode, no curly quotes, no em-dashes. PASS.
- No lock(): No lock() statement anywhere. PASS.
- CYC=5: 5 decision points as annotated. All <= 8. PASS.
- No throw: No throw statements. PASS.
- void return: Execute returns void. PASS.
- JS-033 synchronous: Not async, no await. PASS.

### 3B: `src/PropTraderTools/Features/PttBeSwapAll.cs` (NEW)

```csharp
// src/PropTraderTools/Features/PttBeSwapAll.cs
// B180 DW-B180-BE-SWAP-ALL: immediate BE bracket swap for all accounts.
// Replaces PttGlobalBreakEven.Execute(int) deferred-arm path.
// Pattern: identical to PttGlobalQuickExit.Execute() loop structure.
// CYC=5: flag-gate(1), acc-loop(2), follower-skip(3), flat-skip(4), ExecuteOne(5).
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

        // CYC=3: (1) tickSize guard, (2) RoundToTick inline, (3) PttBreakEvenSwap.Execute.
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

**Verification against RULES_CATALOG:**
- ASCII-only: All string literals are straight ASCII. "[BE-ALL] Blocked:", "[BE-ALL] PttBeSwapAll: firing buf=", "[BE-ALL] swap:" -- PASS.
- No lock(): No lock() statement. PASS.
- CYC=5 (Execute), CYC=3 (ExecuteOne): All <= 8. PASS.
- No throw: No throw statements. PASS.
- void returns. PASS.
- JS-033 synchronous. PASS.

---

## Section 4: CopyEngine.cs Changes (B180-1)

### 4A: SnapshotBeTargetsPublic Wrapper

**Action:** Insert one-line wrapper method immediately after the closing brace of `SnapshotBeTargets`.

**Confirmed source location (read from file):**
- `SnapshotBeTargets` private method: lines 6959-6979
- Line 6959: `private List<(double Price, int Qty, OrderAction Action)> SnapshotBeTargets(`
- Line 6978: `return nativeTargets.Count > 0 ? nativeTargets : pttTargets; // (7)`
- Line 6979: `}` (closing brace of SnapshotBeTargets)
- **Insert after line 6979** (blank line + comment + method body)

**Content to insert after line 6979:**

```csharp

        // B180: public seam for PttBeSwapAll and PttBeSwapOne. Delegates to private SnapshotBeTargets.
        // CYC=1. JS-021: no lock. JS-001: no throw. JS-002: returns List (delegates, never null). ASCII-only.
        internal List<(double Price, int Qty, OrderAction Action)> SnapshotBeTargetsPublic(
            Account acc, Instrument instr)
            => SnapshotBeTargets(acc, instr);
```

**Note on access:** `SnapshotBeTargets` itself remains `private` -- the wrapper provides the `internal` access surface without changing the original method.

**CYC=1:** Single expression body, no branches.

---

## Section 5: TradeCopierPanel.cs Changes (B180-1)

### 5A: OnBeClick Rewrite

**Confirmed source location (read from file):**
- `OnBeClick` starts at line **1480** (confirmed: `private void OnBeClick(object sender, RoutedEventArgs e)`)
- `ExecuteBeIdle` starts at line **1497**, closes at line **1521**
- `ExecuteBeArmed` starts at line **1525**, closes at line **1534**
- **Replace lines 1480-1534** (entire OnBeClick + ExecuteBeIdle + ExecuteBeArmed block)

**BEFORE (lines 1480-1534):**
```csharp
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
```

**AFTER (B180-1 replacement):**
```csharp
        // B180 DW-B180-BE-SWAP-ALL: OnBeClick -- immediate BE bracket swap.
        // Replaces Idle/Armed FSM. Click = fire = done. No armed state.
        // CYC=3: (1) instrument null guard, (2) leader null guard, (3) PttBeSwapOne.Execute.
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
```

**Dead code note (B180-1):** `BeState`, `_beState`, `UpdateBeVisuals`, `ExecuteBeIdle`, `ExecuteBeArmed`, `IsPriceAlreadyAtBe`, `ComputeBeTargetPrice`, `IsPriceAtOrPastTarget` are now unreferenced by `OnBeClick` but are **NOT removed in B180-1**. They are removed in B180-2 after VERIFY_PASS.

### 5B: OnGlobalBeClick Rewrite

**Confirmed source location (read from file):**
- `OnGlobalBeClick` starts at line **1357** (confirmed: `private void OnGlobalBeClick(object sender, RoutedEventArgs e)`)
- Method closes at line **1377** (`}`)
- **Replace lines 1357-1377** (entire OnGlobalBeClick body)

**BEFORE (lines 1357-1377):**
```csharp
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
```

**AFTER (B180-1 replacement):**
```csharp
        // B180 DW-B180-BE-SWAP-ALL: OnGlobalBeClick -- immediate BE swap all accounts.
        // Replaces IsPendingSlotsEmpty + armed-state FSM. Click = fire = done.
        // CYC=1 (straight-line -- no branch; PttBeSwapAll.Execute has its own flag gate).
        // JS-021: no lock. JS-033: synchronous void. JS-001: no throw. ASCII-only.
        private void OnGlobalBeClick(object sender, RoutedEventArgs e)
        {
            PttBeSwapAll.Execute(CopyEngine.Instance.GlobalBe.GlobalBeBuffer);
        }
```

**Dead code note (B180-1):** `IsPendingSlotsEmpty`, `GlobalBeAllDisarmed` event subscription, `PendingBeArmed` event subscription, armed-state visual logic remain compiled but are **NOT removed in B180-1**.

---

## Section 6: B180-2 Dead Code Removal List

**DEPENDENCY: B180-2 MUST NOT START until B180-1 VERIFY_PASS is confirmed by ptt-verifier.**

### 6A: CopyEngine.cs Dead Symbols

All confirmed by reading source. Zero-caller verification required before each removal (grep pattern provided).

| Symbol | Type | Actual Line | Caller Check Before Removal |
|--------|------|-------------|----------------------------|
| `_pendingBeSlots` field | Field | 270-271 | `grep -c "_pendingBeSlots" src/PropTraderTools/CopyEngine.cs` must be 1 (self only) |
| `_pendingBePriceRetry` field | Field | 320-321 | `grep -c "_pendingBePriceRetry" src/PropTraderTools/CopyEngine.cs` must be 1+ self (clear all usages first) |
| `_bePriceRetryAttempts` field | Field | 327-328 | Same grep pattern |
| `BeRetryMaxAttempts` const | Const | 334 | Check no remaining reference |
| `PendingBeFired` event | Event | 450 | Check TradeCopierPanel.cs unsubscribes removed too |
| `PendingBeArmed` event | Event | 451 | Confirmed subscribed at TradeCopierPanel.cs line 811 -- remove subscription first |
| `RaiseBeAllDisarmed()` | Method | 491 | Check TradeCopierPanel line 678 (DisarmBeAllOnFlat) removed |
| `GlobalBeAllDisarmed` event | Event | 489 | Check TradeCopierPanel line 580, 814 removed |
| `ArmAllPendingBe` | Method | 1498-1511 | Caller: PttGlobalBreakEven.Execute(int) -- must be removed first |
| `TryTriggerBeRecovery` | Method | 5222-5243 | Called from OnOrderUpdate -- remove call-site first |
| `LogBeCancelDiag` | Method | ~5313 | Called from TryTriggerBeRecovery -- removed when parent removed |
| `TryReplacePttBeBrackets` | Method | 5338-~5375 | Called from TryTriggerBeRecovery -- removed with parent |
| `MoveStopToBreakEven` | Method | 7069-7171 | After B180-1: no callers remain (was called from ArmPendingBe trigger path). Verify with grep. |
| `ArmPendingBe` | Method | 7558-~7610 | After B180-1: TradeCopierPanel.cs no longer calls it. Verify with grep. |
| `DisarmPendingBe` | Method | 7684-7695 | After B180-1 + TradeCopierPanel dead code removal. Check TradeCopierPanel line 582/667/582 |
| `IsPendingSlotsEmpty` | Method | 7700 | After B180-1: TradeCopierPanel.cs no longer calls it |
| `ClearAllPendingBeSlots` | Method | 7707-7715 | Called at TradeCopierPanel line 586 -- remove call-site first |
| `OnPendingBeAccountUpdate` | Method | 7819-7836 | Registered in RegisterPendingBeSlot -- removed when registration removed |
| `TryFireBePriceRetry` | Method | 7263-~7320 | Called from OnOrderUpdate -- remove call-site first |
| `IsBeBracketRecoveryEligible` | Method | ~5372 | Called from TryReplacePttBeBrackets |
| `HasActivePttBeExits` / `HasActivePttBeTargets` / `HasSettlingBeTargets` | Methods | ~3250+ | Check callers with grep |
| `RegisterBeRetrySlotIfNeeded` | Method | ~7116 | Called from MoveStopToBreakEven |
| `RegisterPendingBeSlot` | Method | ~7584 | Called from ArmPendingBe |
| `TryArmBePriceRetry` | Method | ~7113 | Called from MoveStopToBreakEven |
| `IsBeRetryArmedFlat` / `IsBeRetrySlotNeeded` | Methods | ~3278 | Check callers |
| `StillAtNonBePrice` | Method | ~7291 | Called from TryFireBePriceRetry |
| `IsImmediateBeEligible` / `FireImmediateBe` | Methods | ~7573 | Called from ArmPendingBe |
| `GlobalBeAllDisarmed` event + `RaiseBeAllDisarmed` | Event+Method | 489, 491 | Check all callers removed |
| `PendingBeArmed` event | Event | 451 | Check all callers removed |
| `_globalBe` field + `GlobalBe` property | Field+Property | 166, 667-675 | **KEEP** (see Decision below) |

**Decision -- GlobalBe field retention:**
`_globalBe` (line 166) and `GlobalBe` property (line 667-675) are **KEPT** in B180-2 because:
- `CopyEngine.Instance.GlobalBe.GlobalBeBuffer` is still used in the new `OnGlobalBeClick` (after B180-1 rewrite)
- `CopyEngine.Instance.GlobalBe.IncrementBuffer/DecrementBuffer` are still used in `OnGlobalBeUp/Down` (unchanged)
- `_globalBe` is the backing field for the `GlobalBe` property -- removing it would break the buffer UI

What changes in PttGlobalBreakEven.cs (see Section 6C below).

### 6B: TradeCopierPanel.cs Dead Symbols

| Symbol | Type | Actual Lines | Notes |
|--------|------|-------------|-------|
| `BeState` enum | Enum | 393-397 | Remove after all references gone |
| `_beState` field | Field | 243 | Remove after all references gone |
| `ExecuteBeIdle` | Method | 1497-1521 | Removed in B180-1 already (folded into OnBeClick replacement range 1480-1534) -- **already gone after B180-1** |
| `ExecuteBeArmed` | Method | 1525-1534 | Same -- **already gone after B180-1** |
| `UpdateBeVisuals` | Method | 1582-1597 | Remove after _beState and BeState removed |
| `OnBeConnected` | Method | 1604-1613 | Remove (PendingBeFired event removed from engine) |
| `ResetBeStateOnFlat` | Method | 660-668 | Remove after _beState removed |
| `IsPriceAlreadyAtBe` | Method | 1556-1569 | Remove (no callers after B180-1 removes ExecuteBeIdle) |
| `ComputeBeTargetPrice` | Method | 1539-1546 | Remove (called only by IsPriceAlreadyAtBe) |
| `IsPriceAtOrPastTarget` | Method | 1550-1552 | Remove (called only by IsPriceAlreadyAtBe) |
| `OnPendingBeArmedDispatch` | Method | 1102-1109 | Remove (PendingBeArmed event removed) |
| `OnGlobalBeAllDisarmed` | Method | 1136-1139 | Remove (GlobalBeAllDisarmed event removed) |
| `DisarmBeAllOnFlat` | Method | 672-680 | Remove (calls RaiseBeAllDisarmed and IsPendingSlotsEmpty -- both gone) |
| `UpdateBeAllVisuals` | Method | 1144-~1155 | Remove (called only by dead methods above) |
| Engine event subscriptions (Detach): | | lines 576-580 | Remove lines for PendingBeFired, PendingBeArmed, GlobalBeAllDisarmed |
| Engine event subscriptions (SubscribeEngineEvents): | | lines 810-814 | Remove lines for PendingBeFired, PendingBeArmed, GlobalBeAllDisarmed |
| `OnGlobalBeBufferChanged` wiring to GlobalBeAllDisarmed: | | -- | Verify this subscription is removed |
| `_engine.DisarmPendingBe(_leaderAccount)` in Detach | | line 582 | Remove (DisarmPendingBe removed from engine) |
| `_engine.ClearAllPendingBeSlots()` in Detach | | line 586 | Remove (ClearAllPendingBeSlots removed from engine) |

**Note on ExecuteBeIdle / ExecuteBeArmed:** These methods are physically inside the lines 1480-1534 range that B180-1 replaces with the new OnBeClick body. They are therefore **eliminated by B180-1's apply_diff** -- the engineer does not need to explicitly remove them in B180-2. The B180-2 engineer must only clean up the remaining BeState-related dead symbols listed above.

### 6C: PttGlobalBreakEven.cs -- Gut to Thin Buffer Holder

**Decision: Option A -- Keep PttGlobalBreakEven as thin buffer holder. Do NOT delete the file.**

Rationale: `CopyEngine.GlobalBe.GlobalBeBuffer`, `IncrementBuffer`, `DecrementBuffer` are still used from TradeCopierPanel.cs after B180-1. Deleting the class would require renaming all call-sites. Option A is minimal churn.

**What to REMOVE from PttGlobalBreakEven.cs in B180-2:**
- `_ocoSeq` field (line 23)
- `_submitBeStop` delegate field (line 29)
- Production constructor (lines 34-37) -- no longer needed (no _submitBeStop to inject)
- Test injection constructor (lines 40-43)
- `Execute(int bufferTicks)` body (lines 48-64) -- delete method entirely OR replace body with `// B180: removed -- use PttBeSwapAll.Execute instead`
- `Execute(IEnumerable<Account>, int)` test overload (lines 68-79)
- `ExecuteOne` private method (lines 86-97)
- `BuildGlobalBeOcoId` static method (lines 106-107)

**What to KEEP in PttGlobalBreakEven.cs:**
- `_globalBeBuffer` field (line 18) -- used by GlobalBeBuffer property
- `GlobalBeBuffer` property (line 109) -- used by OnGlobalBeClick, OnGlobalBeUp display
- `IncrementBuffer()` method (lines 111-115) -- used by OnGlobalBeUp
- `DecrementBuffer()` method (lines 118-123) -- used by OnGlobalBeDown

**Post-B180-2 PttGlobalBreakEven.cs structure (for engineer reference):**
```csharp
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
```

**Note:** `RaiseBeBufferChanged` calls `Dispatcher.InvokeAsync(() => GlobalBeBufferChanged?.Invoke(newValue))` in CopyEngine.cs. This event wire and the `OnGlobalBeBufferChanged` handler in TradeCopierPanel.cs must be retained (they keep the "BE ALL +N" label synced across panels).

### 6D: PttBreakEven.cs -- DELETE in B180-2

**Decision: Option A -- Delete the file entirely in B180-2.**

**Rationale:**
After B180-1 replaces `OnBeClick` (lines 1480-1534), the `ExecuteBeIdle` method (which called `DispatchModule("BE")`) is gone. No code path calls `DispatchModule("BE")` or instantiates `PttBreakEven` after B180-1.

**Pre-deletion caller check (required before delete):**
```
grep -c "PttBreakEven\|DispatchModule(\"BE\")" src/PropTraderTools/
```
Must return 0 outside of PttBreakEven.cs itself before the file is deleted.

Also check for `PttBreakEven` registration in CopyEngine's module registry (wherever `DispatchModule` is implemented -- verify no registration remains). The engineer must grep for `"BE"` in the context of module registration and remove any leftover `PttBreakEven` instantiation.

---

## Section 7: B180-3 Tests

**File:** `src/PropTraderTools/CopyEngineTests.cs`
**Test class:** `B180BeSwapTests`

### Test signatures and assertions:

**T-B180-T1-01: PttBeSwapOne -- flat account skips**
```csharp
[Fact]
public void PttBeSwapOne_FlatAccount_SkipsExecution()
{
    // Setup: engine with flat account (pos == null or qty == 0)
    // Call: PttBeSwapOne.Execute(acc, instr, 0)
    // Expected: PttBreakEvenSwap.Execute NOT called (no orders submitted)
    //           StatusUpdate fires with flat-skip message containing ": BE flat skip"
    // Seam: inject StatusUpdate delegate or mock FindPositionPublic to return null/qty=0
}
```

**T-B180-T1-02: PttBeSwapOne -- tick-rounded entry price**
```csharp
[Fact]
public void PttBeSwapOne_LongPosition_ComputesTickRoundedEntryPrice()
{
    // Setup: long position at AveragePrice=4500.125, tickSize=0.25, buf=0
    // Expected: entryPrice = Math.Round(4500.125/0.25)*0.25 = 4500.25 (nearest tick up)
    // Verify: entryPrice passed to PttBreakEvenSwap.Execute equals 4500.25
    // Seam: SnapshotBeTargetsPublic + injected PttBreakEvenSwap delegate or captured output
}
```

**T-B180-T1-03: PttBeSwapOne -- bufferTicks shifts entry price**
```csharp
[Fact]
public void PttBeSwapOne_BufferTicks_ShiftsEntryPriceByDirection()
{
    // Setup long: AveragePrice=5000.0, tickSize=0.25, buf=2
    // Expected long entryPrice: 5000.0 + (-1.0)*2*0.25 = 4999.5
    // Setup short: AveragePrice=5000.0, tickSize=0.25, buf=2
    // Expected short entryPrice: 5000.0 + (+1.0)*2*0.25 = 5000.5
    // Verify both via captured entryPrice passed to PttBreakEvenSwap.Execute
}
```

**T-B180-T1-04: PttBeSwapAll -- follower accounts skipped**
```csharp
[Fact]
public void PttBeSwapAll_FollowerAccounts_AreSkipped()
{
    // Setup: engine with leader account (has position) + follower account (has position)
    // Call: PttBeSwapAll.Execute(0)
    // Expected: PttBreakEvenSwap.Execute called exactly once (leader only, not follower)
    // Seam: IsFollowerAccount injectable or test accounts with known follower state
}
```

**T-B180-T1-05: RoundToTick regression (uses existing seam)**
```csharp
[Fact]
public void RoundToTick_KnownValues_MatchExpected()
{
    // NOTE: Use CopyEngine.RoundToTickTestable (actual name at line 4514)
    // NOT "RoundToTickPublic" (orchestrator prompt has wrong name)
    Assert.Equal(4500.25, CopyEngine.RoundToTickTestable(4500.125, 0.25));
    Assert.Equal(4500.0,  CopyEngine.RoundToTickTestable(4499.875, 0.25));
    // Confirms tick-rounding matches NT8 behavior
    // No NT8 runtime required -- pure math, no Account/Instrument dependencies
}
```

**Note on T-B180-T1-05:** The orchestrator prompt references "RoundToTickPublic" and "CopyEngine.RoundToTickPublic" but the actual method name in `CopyEngine.cs` at line 4514 is `RoundToTickTestable`. The test MUST use `RoundToTickTestable` -- do NOT add a duplicate seam. The second assertion `4999.875 -> 4500.0` in the orchestrator prompt appears to be a typo (4999.875/0.25 = 19999.5, rounds to 20000, * 0.25 = 5000.0, not 4500.0). Corrected assertion: `CopyEngine.RoundToTickTestable(4999.875, 0.25) == 5000.0`.

**Test class structure:**
- No NT8 runtime required for T-B180-T1-05.
- T-B180-T1-01 through T-B180-T1-04 require mocking the CopyEngine seams. Use existing test injection patterns from the CopyEngineTests.cs infrastructure.
- `[Fact]` attribute, `xUnit` framework (matches existing tests in `PropTraderTools.Tests.csproj`).

---

## Section 8: CYC Table

All new and changed methods. All must be <= 8.

| Method | File | Before | After | Delta | CYC <= 8 |
|--------|------|--------|-------|-------|-----------|
| `OnBeClick` | TradeCopierPanel.cs | 4 | 3 | -1 | YES |
| `OnGlobalBeClick` | TradeCopierPanel.cs | 4 | 1 | -3 | YES |
| `PttBeSwapOne.Execute` (NEW) | PttBeSwapOne.cs | -- | 5 | +5 | YES |
| `PttBeSwapAll.Execute` (NEW) | PttBeSwapAll.cs | -- | 5 | +5 | YES |
| `PttBeSwapAll.ExecuteOne` (NEW) | PttBeSwapAll.cs | -- | 3 | +3 | YES |
| `SnapshotBeTargetsPublic` (NEW) | CopyEngine.cs | -- | 1 | +1 | YES |

**CYC accounting notes:**
- `OnBeClick` after: base(1) + _instrument null(1) + _leaderAccount null(1) = 3.
- `OnGlobalBeClick` after: base(1) = 1 (straight-line delegation, no branches).
- `PttBeSwapOne.Execute`: base(1) + flag(1) + instr null(1) + tickSize(1) + pos null(1) = 5. The `?.` and `??` operators in `instr.MasterInstrument?.TickSize ?? 0.0` may add 1-2 points in Lizard's strict counting mode, potentially yielding CYC=6-7 -- still compliant.
- `PttBeSwapAll.Execute`: base(1) + flag(1) + acc-loop foreach(1) + follower-skip continue(1) + pos null continue(1) = 5. Compliant.
- `PttBeSwapAll.ExecuteOne`: base(1) + instr null(1) + tickSize(1) = 3. Compliant.
- `SnapshotBeTargetsPublic`: expression body, no branches = 1. Compliant.

---

## Section 9: JS Rule Compliance

Map of each new/changed method to relevant Jane Street rules:

| Method | JS-021 No Lock | JS-001 No Throw | JS-002 No Return Null | JS-033 Sync Void | ASCII-Only |
|--------|---------------|-----------------|----------------------|-----------------|-----------|
| `OnBeClick` (rewrite) | PASS -- no lock() | PASS -- no throw | PASS -- void return | PASS -- not async | PASS -- ASCII only |
| `OnGlobalBeClick` (rewrite) | PASS | PASS | PASS -- void | PASS | PASS |
| `PttBeSwapOne.Execute` | PASS | PASS | PASS -- void | PASS | PASS |
| `PttBeSwapAll.Execute` | PASS | PASS | PASS -- void | PASS | PASS |
| `PttBeSwapAll.ExecuteOne` | PASS | PASS | PASS -- void | PASS | PASS |
| `SnapshotBeTargetsPublic` | PASS | PASS | PASS -- List delegate | PASS | PASS |

**Rule detail notes:**
- **JS-021 (no lock):** Confirmed zero `lock()` statements in all new and changed code. ConcurrentDictionary operations (used only in existing methods not changed in B180-1) are inherently lock-free.
- **JS-001 (no throw):** No `throw` keyword in any new or changed method. All failure modes use early return.
- **JS-002 (no return null):** All new methods return `void`. `SnapshotBeTargetsPublic` delegates to `SnapshotBeTargets` which returns empty `List<>`, never null (existing behavior confirmed by source comment at line 6967: `return nativeTargets; // JS-002: empty list, never null`).
- **JS-033 (synchronous void):** No `async void` or `async Task` in any new or changed method. All are `void` event handlers or `void` static methods. Called from WPF UI thread button handlers.
- **ASCII-only:** All string literals in new code verified: `"[BE] swap: "`, `"[BE-ALL] swap: "`, `"[BE-ALL] Blocked: Break Even requires Pro tier"`, `"[BE-ALL] PttBeSwapAll: firing buf="`, `"Break Even requires Pro tier"`, `": BE flat skip"` -- all use straight ASCII double quotes, no Unicode characters, no curly quotes, no em-dashes, no special characters.
- **No DateTime.Now:** Not present in any new code. `DateTime.MaxValue` used only in `PttBreakEvenSwap.Execute` (existing, unchanged).
- **No FontFamily:** Not present.
- **No hardcoded hex colors:** Not present.

---

## Section 10: 7-Scan Checklist (Pre-flight)

Scan instructions for the engineer to verify compliance before submitting each ticket.

**SCAN-01 -- No lock():**
```
grep -c "lock(" src/PropTraderTools/CopyEngine.cs
grep -c "lock(" src/PropTraderTools/TradeCopierPanel.cs
grep -c "lock(" src/PropTraderTools/Features/PttBeSwapOne.cs
grep -c "lock(" src/PropTraderTools/Features/PttBeSwapAll.cs
```
Expected: 0 for all. (Existing CopyEngine.cs must already be 0.)

**SCAN-02 -- No throw statements in new/changed methods:**
```
grep -n "throw " src/PropTraderTools/Features/PttBeSwapOne.cs
grep -n "throw " src/PropTraderTools/Features/PttBeSwapAll.cs
```
Expected: 0 matches (no throw in new files).

**SCAN-03 -- ASCII-only in new string literals:**
```
grep -Pn "[^\x00-\x7F]" src/PropTraderTools/Features/PttBeSwapOne.cs
grep -Pn "[^\x00-\x7F]" src/PropTraderTools/Features/PttBeSwapAll.cs
```
Expected: 0 matches.

**SCAN-04 -- No DateTime.Now:**
```
grep -c "DateTime.Now" src/PropTraderTools/Features/PttBeSwapOne.cs
grep -c "DateTime.Now" src/PropTraderTools/Features/PttBeSwapAll.cs
grep -c "DateTime.Now" src/PropTraderTools/CopyEngine.cs
grep -c "DateTime.Now" src/PropTraderTools/TradeCopierPanel.cs
```
Expected: 0 for all.

**SCAN-05 -- B180-1 additive verification (OnBeClick/OnGlobalBeClick rewired):**
```
grep -c "PttBeSwapOne" src/PropTraderTools/TradeCopierPanel.cs
grep -c "PttBeSwapAll" src/PropTraderTools/TradeCopierPanel.cs
grep -c "ArmPendingBe" src/PropTraderTools/TradeCopierPanel.cs
grep -c "IsPendingSlotsEmpty" src/PropTraderTools/TradeCopierPanel.cs
grep -c "SnapshotBeTargetsPublic" src/PropTraderTools/CopyEngine.cs
```
Expected: PttBeSwapOne=1, PttBeSwapAll=1, ArmPendingBe=0, IsPendingSlotsEmpty=0, SnapshotBeTargetsPublic>=1.

**SCAN-06 -- B180-2 dead code removal verification:**
```
grep -rc "ArmPendingBe\|MoveStopToBreakEven\|TryTriggerBeRecovery\|_pendingBeSlots" src/
grep -c "lock(" src/PropTraderTools/CopyEngine.cs
```
Expected: all patterns return 0, lock() returns 0.

**SCAN-07 -- Build and test clean:**
```
dotnet build
dotnet test
powershell -File .\scripts\ptt-sync.ps1
```
Expected: 0 build errors, 0 new test failures, ptt-sync.ps1 0 errors after every ticket.

---

## Section 11: Decisions Summary

| Decision | Choice | Rationale |
|----------|--------|-----------|
| GlobalBe buffer holder (B180-2) | **Option A: Keep PttGlobalBreakEven as thin buffer holder** | Avoids rename cascade on 5 call-sites. Minimal churn. |
| PttBreakEven.cs fate (B180-2) | **Option A: Delete the file** | Zero callers after B180-1. Clean deletion. Confirm with grep before delete. |
| RoundToTick test seam | **Use `RoundToTickTestable` (line 4514)** | Orchestrator prompt says "RoundToTickPublic" but actual method is `RoundToTickTestable`. Do NOT add duplicate seam. |
| SnapshotBeTargets access | **Wrapper pattern -- original stays private** | Adds `SnapshotBeTargetsPublic` as thin wrapper. Zero change to `SnapshotBeTargets` internals. |
| Staged pipeline | **B180-1 additive, B180-2 removal, B180-3 tests -- strictly sequential** | Orchestrator mandate. SCOPE GATE enforced. |

---

## Section 12: Deferred Backlog Status (Carry-Forward)

| ID | Block | Description | Priority | Status |
|----|-------|-------------|----------|--------|
| DW-B180-BE-SWAP-ALL | B180 | Replace deferred BE/BE ALL with immediate bracket swap. B180-1 (additive), B180-2 (removal), B180-3 (tests). | P1 | **IN PROGRESS -- target of B180** |
| DW-B180-SIM | B180 | SIM GATE: Director verifies BE swap fires immediately, idempotent re-click, flatten-after-BE no ghost re-arm. | P1 | OPEN -- SIM pending post-B180 |
| DW-B179-BE-ALL-SILENT-CANCEL | B179 | SUPERSEDED by B180 (root cause eliminated). B179 prompt NOT required if B180 ships. | P1 | SUPERSEDED |
| DW-B177-CYC | B177 | OnAtrBoxPreviewKeyDown CYC=13 (TradeCopierPanel.cs lines 3318-3344). Extract InjectAtrChar(char c) helper. | P2 | OPEN -- carry forward |
| DW-B170-SIM | B170 | SIM GATE: Director drags target on leader, confirm follower stop preserved. | P0 | OPEN (PENDING SIM GATE) |
| DW-B169-K1 | B169 | SIM GATE: 3-stop simultaneous drag -- 0 missing follower stops | P0 | OPEN (Sim102 blocked by B167-DEFER-02) |
| DW-B169-K2 | B169 | SIM GATE: single-stop drag regression | P0 | OPEN |
| DW-B168-K1 | B168 | Superseded by DW-B169-K1/K2 | P0 | OPEN (superseded) |
| B167-DEFER-02 | B167 | fo=NULL follower ATM brackets never placed after leader fill. Blocks DW-B169-K1 Sim102. | P1 | OPEN |
| B167-DEFER-01 | B167 | ECA-1 double-position race. | P2 | OPEN |
| B167-DEFER-03 | B167 | Combined B161-B166 SIM gate | P1 | OPEN |
| B167-DEFER-04 | B167 | B167 SIM gate | P1 | OPEN |
| DW-B159-K2 | B159 | 14 xUnit tests | P2 | OPEN |
| DW-B159-K3 | B159 | Attempt cap hardcoded at 3 | P2 | OPEN |
| DW-B159-K4 | B159 | intendedBePrice sourcing | P2 | OPEN |

---

## Appendix A: ptt-sync.ps1 Requirement

After ALL edits in each ticket (B180-1, B180-2, B180-3), the engineer MUST run:
```
powershell -File .\scripts\ptt-sync.ps1
```
Expected output: `N linked, 0 errors` (N = number of hard-linked files).

---

`PLAN_COMPLETE`
