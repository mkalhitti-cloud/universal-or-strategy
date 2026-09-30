# B180 PTT-Orchestrator Prompt
# BLOCK: B180 -- DW-B180-BE-SWAP-ALL: Replace BE / BE ALL with immediate bracket-swap to entry price
# Produced by: ptt-orchestrator (copier-spec mode)
# Status: AWAITING DIRECTOR GO

---

## MISSION STATEMENT

Block B180 replaces the deferred-wait BE and BE ALL buttons with an immediate
bracket-swap design. The new buttons occupy the same slots in the panel layout
(same position, same field: `_beBtn2` / `_globalBeBtn2`), reuse the same +/- spin
controls, and keep the same button label format. The only visible change is the
label text: "BE" stays "BE" and "BE ALL" stays "BE ALL". The internal behaviour
changes completely:

**Old (deferred):**  arm slot → wait for price to return to entry → then move stop

**New (immediate):**  button click → cancel existing brackets → submit new limits
at entry price per tranche → done. Synchronous. No armed state. No slots.

This closes DW-B179-BE-ALL-SILENT-CANCEL at root (the race cannot occur if there is
no armed state to race against) and eliminates the entire `_pendingBeSlots`,
`_pendingBePriceRetry`, `ArmPendingBe`, `DisarmPendingBe`, `BeState` FSM,
`GlobalBeAllDisarmed`, and `PendingBeArmed` event infrastructure.

**Entry price used:** `pos.AveragePrice` rounded to tick via `RoundToTick`.
Director confirmed: use average price (NT8 `Position.AveragePrice`), ticked.
Buffer ticks (+/- spin) are kept as an offset: long BE stop = entry - buf * tick,
short BE stop = entry + buf * tick. Same sign convention as today. At buf=0 the
stop is placed exactly at entry (ticked).

**What is kept:** The `PttBreakEvenSwap.Execute` engine (already immediate, already
correct). The `_beBuffer` / `GlobalBe.GlobalBeBuffer` spin state. The `_beBtn2` /
`_globalBeBtn2` button references. The `BrushTeal` / `BrushActive` visual states.
The `BreakEven` feature flag gate. `UpdateBeLabel`, `UpdateBeAllVisuals`.

**What is removed:** `BeState` enum, `_beState` field, `ArmPendingBe`,
`DisarmPendingBe`, `ArmAllPendingBe`, `OnPendingBeAccountUpdate`,
`_pendingBeSlots`, `_pendingBePriceRetry`, `_bePriceRetryAttempts`,
`PttGlobalBreakEven.Execute(int)`, `PttGlobalBreakEven` armed-state logic,
`GlobalBeAllDisarmed` event + `RaiseBeAllDisarmed`, `PendingBeArmed` event,
`ResetBeStateOnFlat`, `OnBeConnected`, `ExecuteBeIdle` / `ExecuteBeArmed`,
`IsPendingSlotsEmpty`, `IsPriceAlreadyAtBe`, `TryTriggerBeRecovery`,
`TryReplacePttBeBrackets`, `TryFireBePriceRetry`, `LogBeCancelDiag`,
`TryEmitBeWarnIfFlatArmed` (B179 -- no longer needed), `MoveStopToBreakEven`,
`PttBreakEven` module + `DispatchModule("BE")` path, `RegisterBeRetrySlotIfNeeded`,
`RegisterPendingBeSlot`, `ClearAllPendingBeSlots`.

**IMPORTANT -- SCOPE GATE:** This is a large removal. The pipeline MUST work in
stages: **B180-1 rewires the buttons (additive only -- new click handlers), B180-2
removes dead code**. The architect MUST NOT combine both into one ticket. Additive
change ships and verifies first; removal ships and verifies second. This prevents
a broken intermediate state.

Files under surgery:
  `src/PropTraderTools/Features/PttBreakEven.cs`        (keep, repurpose)
  `src/PropTraderTools/Features/PttGlobalBreakEven.cs`  (keep, gut)
  `src/PropTraderTools/TradeCopierPanel.cs`             (rewire handlers)
  `src/PropTraderTools/CopyEngine.cs`                   (remove dead code in B180-2)
  `src/PropTraderTools/CopyEngineTests.cs`              (update tests)
Pipeline: FULL 5-PHASE (all phases mandatory, none skippable, none combinable)

---

## BUG / DESIGN DESCRIPTION

### DW-B180-BE-SWAP-ALL (P1 -- Architectural Replacement)

**Problem (confirmed from 2026-09-28 live session):**
The current BE / BE ALL buttons are deferred-wait mechanisms. They arm a price
watcher and wait for price to return to entry before moving the stop. This creates:
- Silent no-op when user flattens before price returns (DW-B179)
- Stale `_pendingBeSlots` entries that can ghost-fire on subsequent trades
- Multiple known races: QX→BE-ALL, ATM-sweep, reconnect, _bePriceRetryAttempts
- ~40 methods of race-management complexity in CopyEngine.cs

**Solution:**
Replace with synchronous immediate bracket swap. On click:
1. `SnapshotBeTargets` (same as today -- gets current target count/qty per tranche)
2. `PttBreakEvenSwap.Execute(acc, instr, entryPrice, targets)` -- cancel all brackets,
   submit new PTT-BE-Stop at entry + new PTT-BE-Target at existing target prices
3. Done. No slot. No subscription. No waiting.

**Entry price calculation (confirmed):**
```csharp
double tickSize  = instr.MasterInstrument.TickSize;
double entryRaw  = pos.AveragePrice + (isLong ? -1.0 : +1.0) * bufferTicks * tickSize;
double entryPrice = Math.Round(entryRaw / tickSize) * tickSize; // = RoundToTick(entryRaw, tickSize)
```
Same formula as `MoveStopToBreakEven` line 7092-7094. At `bufferTicks=0`, `entryPrice = RoundToTick(pos.AveragePrice, tickSize)`.

**Per-chart BE button (single account):**
- Button label: `"BE +N"` / `"BE -N"` (unchanged -- FormatBuffer("BE", _beBuffer))
- On click: call new `PttBreakEvenSwap` path for `_leaderAccount` + `_instrument`
- No FSM state. No Armed state. Click = fire = done. Idempotent.
- Flat guard: if leader is flat, log `[BE] flat skip -- <acc>`, return.

**BE ALL button (all accounts):**
- Button label: `"BE ALL +N"` / `"BE ALL"` (unchanged -- FormatGlobalBeBuffer)
- On click: loop `Account.All`, skip followers, skip flat accounts, call
  `PttBreakEvenSwap` path for each non-flat non-follower account
- No armed state. No `IsPendingSlotsEmpty` check. Click = fire = done. Idempotent.
- After fire: button stays teal (not amber -- there is no "armed" state to show).

**Visual states (simplified):**
- `BeState` enum and `_beState` field are **removed** in B180-2.
- Until B180-2: `_beState` stays but `OnBeClick` never sets it to Armed.
  `UpdateBeVisuals` is called with `BeState.Idle` only (button never goes amber).
- After B180-2: `BeState`, `_beState`, `UpdateBeVisuals`, `OnBeConnected`,
  `ResetBeStateOnFlat` are all removed.

---

## CONFIRMED SOURCE EVIDENCE (do not re-read from files -- embed directly)

```
TradeCopierPanel.cs line 1194-1195 (button spec table):
    ("BE",    ..., OnBeUp, OnBeDown, OnBeClick, b => _beBtn2 = b, _beRowPanel),
    ("BE ALL",..., OnGlobalBeUp, OnGlobalBeDown, OnGlobalBeClick, b => _globalBeBtn2 = b, _beRowPanel)

TradeCopierPanel.cs line 1357-1377 (OnGlobalBeClick -- current):
    private void OnGlobalBeClick(object sender, RoutedEventArgs e)
    {
        if (CopyEngine.Instance.IsPendingSlotsEmpty())
            CopyEngine.Instance.GlobalBe.Execute(CopyEngine.Instance.GlobalBe.GlobalBeBuffer);
        else
            return; // double-press guard
    }

TradeCopierPanel.cs line 1480-1533 (OnBeClick + ExecuteBeIdle + ExecuteBeArmed -- current):
    private void OnBeClick(...) { ... if (Idle) ExecuteBeIdle; else ExecuteBeArmed; }
    private void ExecuteBeIdle(...) { arm or immediate-fire depending on price }
    private void ExecuteBeArmed(...) { disarm }

CopyEngine.cs line 7069-7171 (MoveStopToBreakEven -- current):
    Reads pos.AveragePrice, applies direction + bufferTicks * tickSize, RoundToTick.
    Calls PttBreakEvenSwap.Execute(acc, instrument, newStop, targets).

CopyEngine.cs line 4508-4510 (RoundToTick):
    private static double RoundToTick(double rawPrice, double tickSize)
        => tickSize > 0 ? Math.Round(rawPrice / tickSize) * tickSize : rawPrice;

PttBreakEvenSwap.cs line 65-109 (Execute -- current):
    Accepts (Account acc, Instrument instr, double newStop,
             List<(double Price, int Qty, OrderAction Action)> targets)
    Flat guard -> CancelQxBrackets -> direction -> 0-targets or with-targets OCO loop.

CopyEngine.cs line 6959 (SnapshotBeTargets signature):
    private List<(double Price, int Qty, OrderAction Action)> SnapshotBeTargets(Account acc, ...)
    NOTE: SnapshotBeTargets is PRIVATE. It must be made internal (or a new internal wrapper
    created) for the new PttBeSwapAll class to call it. See Method A below.

TradeCopierPanel.cs line 393-397 (BeState enum -- to be removed in B180-2):
    internal enum BeState { Idle, Armed }

TradeCopierPanel.cs line 240, 243 (fields):
    private int _beBuffer = 1;
    private BeState _beState = BeState.Idle;

TradeCopierPanel.cs line 660-665 (ResetBeStateOnFlat):
    private void ResetBeStateOnFlat(bool hasPosition)
    {
        if (!hasPosition && _beState != BeState.Idle) { _beState = BeState.Idle; UpdateBeVisuals(BeState.Idle); }
    }

PttGlobalBreakEven.cs line 48-64 (Execute(int bufferTicks) -- current):
    CopyEngine.Instance.ArmAllPendingBe(bufferTicks);   <- THIS gets replaced
```

---

## REPAIR DESIGN

### New class: PttBeSwapAll (new file -- src/PropTraderTools/Features/PttBeSwapAll.cs)

Modelled exactly on `PttGlobalQuickExit`. Synchronous. No armed state.

```csharp
// B180 DW-B180-BE-SWAP-ALL: immediate BE bracket swap for all accounts.
// Replaces PttGlobalBreakEven.Execute(int) deferred-arm path.
// Pattern: identical to PttGlobalQuickExit.Execute() loop structure.
// CYC=5: flag-gate(1), acc-loop(2), follower-skip(3), flat-skip(4), ExecuteOne(5).
// JS-021: no lock. JS-001: no throw. JS-002: void. JS-033: synchronous void. ASCII-only.
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

    // CYC=3: (1) tickSize guard, (2) RoundToTick, (3) PttBreakEvenSwap.Execute.
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
```

**Note on `SnapshotBeTargetsPublic`:** `SnapshotBeTargets` in CopyEngine.cs is
currently `private`. B180-1 must expose it as `internal` via a one-line wrapper:
```csharp
// B180: public seam for PttBeSwapAll. Delegates to private SnapshotBeTargets.
// CYC=1. JS-021: no lock. JS-001: no throw. JS-002: returns List. ASCII-only.
internal List<(double Price, int Qty, OrderAction Action)> SnapshotBeTargetsPublic(
    Account acc, Instrument instr)
    => SnapshotBeTargets(acc, instr);
```

---

### Per-chart BE button: OnBeClick rewrite (TradeCopierPanel.cs)

**BEFORE (lines 1480-1533):** Complex FSM with Idle/Armed/ExecuteBeIdle/ExecuteBeArmed.

**AFTER (B180-1 -- OnBeClick, CYC=3):**
```csharp
// B180 DW-B180-BE-SWAP-ALL: OnBeClick -- immediate BE bracket swap.
// Replaces Idle/Armed FSM. Click = fire = done. No armed state.
// CYC=3: (1) instrument null guard, (2) leader null guard, (3) flag gate (inside PttBeSwapOne).
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

---

### New class: PttBeSwapOne (new file -- src/PropTraderTools/Features/PttBeSwapOne.cs)

```csharp
// B180 DW-B180-BE-SWAP-ALL: per-chart immediate BE bracket swap.
// Replaces PttBreakEven module (DispatchModule("BE") path).
// Pattern: mirrors PttBeSwapAll.ExecuteOne but scoped to one account.
// CYC=5: (1) flag-gate, (2) instr null, (3) tickSize guard, (4) flat guard, (5) PttBreakEvenSwap.
// JS-021: no lock. JS-001: no throw. JS-002: void. JS-033: synchronous void. ASCII-only.
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
```

---

### BE ALL button: OnGlobalBeClick rewrite (TradeCopierPanel.cs)

**BEFORE (lines 1357-1377):** IsPendingSlotsEmpty check + CopyEngine.Instance.GlobalBe.Execute.

**AFTER (B180-1 -- OnGlobalBeClick, CYC=1):**
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

---

### Visual state simplification (B180-1)

`UpdateBeVisuals` is called with `BeState.Idle` in every click path. The button
never turns amber. In B180-1 this is achieved by simply not setting `_beState` to
Armed anywhere. The `BeState` enum, `_beState`, and `UpdateBeVisuals` are left in
place but become dead code -- removed in B180-2.

`UpdateBeAllVisuals` similarly never called with `BeState.Armed` after B180-1.
Left in place, removed in B180-2.

`ResetBeStateOnFlat` becomes a no-op (condition `_beState != BeState.Idle` is
always false after B180-1). Left in place, removed in B180-2.

---

### CYC Summary

| Method / Class | File | Before | After | Delta | Compliant |
|---|---|---|---|---|---|
| OnBeClick | TradeCopierPanel.cs | 4 | 3 | -1 | YES |
| OnGlobalBeClick | TradeCopierPanel.cs | 4 | 1 | -3 | YES |
| PttBeSwapOne.Execute (NEW) | PttBeSwapOne.cs | -- | 5 | +5 | YES |
| PttBeSwapAll.Execute (NEW) | PttBeSwapAll.cs | -- | 5 | +5 | YES |
| PttBeSwapAll.ExecuteOne (NEW) | PttBeSwapAll.cs | -- | 3 | +3 | YES |
| SnapshotBeTargetsPublic (NEW) | CopyEngine.cs | -- | 1 | +1 | YES |

---

## TICKETS

### B180-1 -- New classes + button rewire (ADDITIVE ONLY -- no removal)

**New files to create:**
- `src/PropTraderTools/Features/PttBeSwapOne.cs` -- per-chart immediate BE swap
- `src/PropTraderTools/Features/PttBeSwapAll.cs` -- all-accounts immediate BE swap

**Edits to existing files:**

**A. CopyEngine.cs -- add `SnapshotBeTargetsPublic` wrapper**
Location: immediately after the closing brace of `SnapshotBeTargets` (line ~7067).
Insert the one-line delegate wrapper shown in the design above.
`SnapshotBeTargets` itself: change access modifier from `private` to `private`
(NO CHANGE -- wrapper delegates, no access change needed).

**B. TradeCopierPanel.cs -- rewrite `OnBeClick`**
Replace lines 1480-1533 (OnBeClick + ExecuteBeIdle + ExecuteBeArmed) with the
3-branch version shown in the AFTER block above.
`_beState`, `BeState`, `UpdateBeVisuals` remain -- they are now unreferenced dead
code but NOT removed in this ticket.

**C. TradeCopierPanel.cs -- rewrite `OnGlobalBeClick`**
Replace lines 1357-1377 with the 1-branch version shown in the AFTER block above.
`IsPendingSlotsEmpty`, `GlobalBeAllDisarmed`, armed-state logic remain as dead code
-- NOT removed in this ticket.

**Constraints:**
- Use `apply_diff` ONLY (no `write_file` on existing .cs files)
- New files: `write_file` is acceptable (new file creation)
- After ALL edits: run `powershell -File .\scripts\ptt-sync.ps1`
- `dotnet build` must pass 0 errors after B180-1

**Verify B180-1:**
- `grep -c "PttBeSwapOne" src/PropTraderTools/TradeCopierPanel.cs` returns 1
- `grep -c "PttBeSwapAll" src/PropTraderTools/TradeCopierPanel.cs` returns 1
- `grep -c "ArmPendingBe" src/PropTraderTools/TradeCopierPanel.cs` returns 0
  (OnBeClick no longer calls ArmPendingBe)
- `grep -c "IsPendingSlotsEmpty" src/PropTraderTools/TradeCopierPanel.cs` returns 0
  (OnGlobalBeClick no longer calls IsPendingSlotsEmpty)
- `grep -c "SnapshotBeTargetsPublic" src/PropTraderTools/CopyEngine.cs` returns 1
- `dotnet build` 0 errors. `dotnet test` 0 new failures.

---

### B180-2 -- Dead code removal (SEPARATE TICKET -- runs AFTER B180-1 VERIFY_PASS)

**DEPENDENCY:** B180-2 MUST NOT START until B180-1 has VERIFY_PASS.

**Symbols to remove from CopyEngine.cs:**
- `ArmAllPendingBe` (line 1498)
- `ArmPendingBe` (line 7558)
- `DisarmPendingBe` (line 7684)
- `DisarmTrailBe` -- KEEP (trail BE is a separate feature, not removed)
- `OnPendingBeAccountUpdate` (line 7819)
- `ClearAllPendingBeSlots` (line 7707)
- `IsPendingSlotsEmpty` (line 7700)
- `_pendingBeSlots` field (line 270)
- `_pendingBePriceRetry` field (line 320)
- `_bePriceRetryAttempts` field (line ~325)
- `MoveStopToBreakEven` (line 7069) -- and its helpers:
    `LogDiagOrderCount`, `SnapshotBeTargets` (but keep `SnapshotBeTargetsPublic` +
    move its content inline, or keep `SnapshotBeTargets` as internal and keep
    `SnapshotBeTargetsPublic` -- architect must decide minimal approach)
- `TryTriggerBeRecovery` (line 5222)
- `TryReplacePttBeBrackets` (line 5338)
- `TryFireBePriceRetry` (line 7263)
- `LogBeCancelDiag` (line 5313)
- `TryEmitBeWarnIfFlatArmed` (B179 -- never landed, but if it did, remove here)
- `IsBeBracketRecoveryEligible` helper
- `HasActivePttBeExits` / `HasActivePttBeTargets` / `HasSettlingBeTargets`
- `RegisterBeRetrySlotIfNeeded` / `RegisterPendingBeSlot`
- `TryArmBePriceRetry`
- `IsBeRetryArmedFlat` / `IsBeRetrySlotNeeded`
- `StillAtNonBePrice`
- `GlobalBeAllDisarmed` event + `RaiseBeAllDisarmed`
- `PendingBeArmed` event
- `_globalBe` lazy field + `GlobalBe` property (BUT KEEP `GlobalBe.GlobalBeBuffer`
  and buffer spin methods -- the +/- spin on BE ALL still uses `GlobalBe` object.
  Alternatively: promote `_globalBeBuffer` to CopyEngine directly. Architect decides
  minimal path.)

**Symbols to remove from TradeCopierPanel.cs:**
- `BeState` enum + `_beState` field
- `ExecuteBeIdle` + `ExecuteBeArmed`
- `UpdateBeVisuals`
- `OnBeConnected`
- `ResetBeStateOnFlat`
- `IsPriceAlreadyAtBe` (and helpers: `ComputeBeTargetPrice`, `GetBeRefPrice`)
- `OnGlobalBeBufferChanged` wiring to `GlobalBeAllDisarmed` subscription

**Symbols to remove from PttGlobalBreakEven.cs:**
- `Execute(int bufferTicks)` body -- replace with delegation to `PttBeSwapAll.Execute`
  OR delete the class entirely and update the one reference in `TradeCopierPanel.cs`
  that calls `CopyEngine.Instance.GlobalBe.IncrementBuffer` / `DecrementBuffer`.
  Architect must decide: keep GlobalBe as a thin buffer-holder, or inline the buffer
  into CopyEngine directly.

**Symbols to remove from PttBreakEven.cs:**
- Entire class (no longer called -- `DispatchModule("BE")` path gone).
  OR: keep file, gut body to a stub with a comment. Architect decides.

**Constraint:** Removal must not break `dotnet build`. Every removed symbol must have
zero remaining references before deletion. Use grep to confirm before each removal.

**Verify B180-2:**
- `grep -rc "ArmPendingBe\|MoveStopToBreakEven\|TryTriggerBeRecovery\|_pendingBeSlots" src/` returns 0
- `grep -c "lock(" src/PropTraderTools/CopyEngine.cs` returns 0 (no new locks)
- `dotnet build` 0 errors. `dotnet test` 0 new failures.
- `powershell -File .\scripts\ptt-sync.ps1` run after all edits.

---

### B180-3 -- Tests

**File:** `src/PropTraderTools/CopyEngineTests.cs`

**Tests to add (class `B180BeSwapTests`):**

**T-B180-T1-01 -- PttBeSwapOne: flat account skips**
Setup: engine with flat account. Call `PttBeSwapOne.Execute(acc, instr, 0)`.
Expected: `PttBreakEvenSwap.Execute` NOT called (no orders submitted).
StatusUpdate emits flat-skip message.

**T-B180-T1-02 -- PttBeSwapOne: calls PttBreakEvenSwap with tick-rounded entry**
Setup: engine with long position at AveragePrice=4500.125, tickSize=0.25, buf=0.
Call `PttBeSwapOne.Execute(acc, instr, 0)`.
Expected: entryPrice passed to PttBreakEvenSwap = `Math.Round(4500.125/0.25)*0.25`
= 4500.25 (nearest tick). Confirm via injected delegate or test seam.

**T-B180-T1-03 -- PttBeSwapOne: bufferTicks shifts entry price**
Setup: long position at AveragePrice=5000.0, tickSize=0.25, buf=2.
Expected entryPrice: `5000.0 + (-1.0) * 2 * 0.25 = 4999.5` (long: stop below entry).
Short position at 5000.0, buf=2: `5000.0 + (+1.0) * 2 * 0.25 = 5000.5`.

**T-B180-T1-04 -- PttBeSwapAll: follower accounts skipped**
Setup: engine with leader account (has position) + follower account (has position).
Call `PttBeSwapAll.Execute(0)`.
Expected: `PttBreakEvenSwap.Execute` called once (leader only), not twice.

**T-B180-T1-05 -- RoundToTick regression**
Call `CopyEngine.RoundToTickPublic(4500.125, 0.25)` = 4500.25.
Call `CopyEngine.RoundToTickPublic(4999.875, 0.25)` = 4500.0 (nearest tick down).
Confirms tick-rounding matches NT8 behavior.
(Note: `RoundToTickPublic` test seam already exists at CopyEngine.cs line 4513 --
use it, do not add a duplicate.)

**No NT8 runtime required. `dotnet build` and `dotnet test` 0 new failures.**

---

## MANDATORY PIPELINE (7-STEP -- ALL PHASES MANDATORY -- NONE SKIPPABLE -- NONE COMBINABLE)

```
Ph1  ptt-architect       Input: this prompt
                         Output: docs/brain/B180/02-architecture-plan.md
                         Contract:
                           - Exact new file content for PttBeSwapOne.cs + PttBeSwapAll.cs
                           - Exact before/after for OnBeClick (TradeCopierPanel.cs)
                           - Exact before/after for OnGlobalBeClick (TradeCopierPanel.cs)
                           - Exact SnapshotBeTargetsPublic wrapper (CopyEngine.cs)
                           - Decision on GlobalBe buffer holder (keep thin vs inline)
                           - Decision on PttBreakEven.cs fate (keep stub vs delete)
                           - CYC table for all changed/new methods
                           - Explicit list of dead-code symbols for B180-2 with line numbers
                           - Rationale for 2-ticket staged approach (additive first, remove second)
                         Gate: none -- proceed to Ph2.

Ph2  ptt-plan-reviewer   Input: 02-architecture-plan.md
                         Output: docs/brain/B180/02-plan-review.md
                         Gate: REVIEW_PASS required. Any REVIEW_FAIL sends back to Ph1.
                         Key checks:
                           - B180-1 and B180-2 are SEPARATE tickets (not combined)
                           - No deferred-arm logic leaks into new code
                           - CYC all methods <= 8
                           - No lock() introduced
                           - ASCII-only all string literals

Ph3  ptt-architect       Input: 02-plan-review.md (REVIEW_PASS)
                         Output: docs/brain/B180/04-tickets.md
                         Contract: 3 tickets (B180-1, B180-2, B180-3) with exact line
                           numbers, exact before/after or full file content for new files,
                           exact removal list with line ranges for B180-2.

Ph3.5 ptt-ticket-reviewer Input: 04-tickets.md
                          Output: docs/brain/B180/04-ticket-review.md
                          Gate: TICKET_REVIEW_PASS required. Any fail sends back to Ph3.

Ph4a ptt-engineer         Input: 04-ticket-review.md (TICKET_REVIEW_PASS) + 04-tickets.md
                          Output: edits to .cs files + new .cs files
                                  + docs/brain/B180/ticket-N-completion.md for each ticket
                          Constraints:
                            - apply_diff for existing files, write_file for new files only
                            - STRICT ORDER: B180-1 complete + verified, THEN B180-2, THEN B180-3
                            - After ALL tickets: run powershell -File .\scripts\ptt-sync.ps1

Ph4b ptt-verifier         Input: ticket-N-completion.md
                          Output: docs/brain/B180/ticket-N-verification.md
                          Checks:
                            B180-1:
                              grep -c "PttBeSwapOne" TradeCopierPanel.cs == 1
                              grep -c "PttBeSwapAll" TradeCopierPanel.cs == 1
                              grep -c "ArmPendingBe" TradeCopierPanel.cs == 0
                              grep -c "IsPendingSlotsEmpty" TradeCopierPanel.cs == 0
                              grep -c "SnapshotBeTargetsPublic" CopyEngine.cs >= 1
                              dotnet build 0 errors
                            B180-2:
                              grep -rc "ArmPendingBe|MoveStopToBreakEven|TryTriggerBeRecovery|_pendingBeSlots" src/ == 0
                              grep -c "lock(" CopyEngine.cs == 0
                              dotnet build 0 errors
                            B180-3:
                              dotnet test 0 new failures
                              ptt-sync.ps1 run after all edits
                          Gate: VERIFY_PASS per ticket. VERIFY_FAIL = targeted fix loop.

Ph5  ptt-plan-reviewer    Input: all ticket-N-verification.md (all VERIFY_PASS)
                          Output: docs/brain/B180/05-final-review.md
                                  docs/brain/B180/06-deferred-backlog.md
                          Contract for 06-deferred-backlog.md:
                            - DW-B180-BE-SWAP-ALL: CLOSED if all 3 VERIFY_PASS
                            - DW-B180-SIM: NEW -- Director SIM gate (see below)
                            - DW-B179-BE-ALL-SILENT-CANCEL: CLOSED by B180 (root cause eliminated;
                              B179 prompt superseded -- do NOT run B179 if B180 ships first)
                            - DW-B177-CYC: carry forward
                            - All prior open items from B177/06-deferred-backlog.md
                              carried forward with updated status
                          Gate: FINAL_PASS or FINAL_FAIL.
```

---

## SIM GATE (post-B180)

1. Enter long position on Sim101.
2. Click per-chart BE button (buf=0). Expected:
   - `[BE] swap: Sim101 entry=<entryPrice> targets=N` in Output.
   - Existing ATM brackets cancelled, new PTT-BE-Stop at entry + PTT-BE-Target
     at original target prices visible in Order Entry grid.
   - Button stays teal (no amber -- no armed state).
3. Click BE button again immediately. Expected: idempotent -- replaces existing
   PTT-BE-Stop/Target with new ones at same price. No error.
4. Enter position on all 4 accounts. Click BE ALL button.
   Expected: all 4 accounts get PTT-BE-Stop at entry simultaneously.
   Output shows `[BE-ALL] swap: <acct> entry=<price> targets=N` for each account.
5. While PTT-BE-Stop/Target orders are working, flatten via Positions tab.
   Expected: NT8 cancels the working orders normally. No ghost re-arm.
   No `[BE-WARN]`. No stale slot. Re-clicking BE ALL on next position works cleanly.

Pass criteria: steps 2-5 all pass. Close DW-B180-SIM upon Director confirmation.

---

## JANE STREET COMPLIANCE CHECKLIST

- [ ] No `lock()` introduced in any new or modified file
- [ ] No Unicode, curly quotes, or non-ASCII in any changed string literal
- [ ] No `DateTime.Now` or `FontFamily` introduced
- [ ] CYC <= 8 for all new/changed methods (see CYC table)
- [ ] `apply_diff` for existing files, `write_file` for new files only
- [ ] B180-1 ships and verifies BEFORE B180-2 starts (staged tickets)
- [ ] Every new string literal: straight double quotes, ASCII only
- [ ] `ptt-sync.ps1` run after all edits
- [ ] `dotnet build` 0 errors, `dotnet test` 0 new failures after each ticket

---

## DEFERRED BACKLOG CARRY-FORWARD (from B177/06-deferred-backlog.md)

| ID | Block | Description | Priority | Status |
|----|-------|-------------|----------|--------|
| DW-B180-BE-SWAP-ALL | B180 | Replace deferred BE/BE ALL with immediate bracket swap to entry price. 3 tickets: B180-1 (additive rewire), B180-2 (dead code removal), B180-3 (tests). | P1 | OPEN -- target of B180 |
| DW-B180-SIM | B180 | SIM GATE: Director verifies BE swap fires immediately, idempotent re-click works, flatten-after-BE has no ghost re-arm. | P1 | OPEN -- SIM pending |
| DW-B179-BE-ALL-SILENT-CANCEL | B179 | SUPERSEDED by B180 (root cause eliminated). B179 prompt NOT required if B180 ships. | P1 | SUPERSEDED |
| DW-B177-CYC | B177 | OnAtrBoxPreviewKeyDown CYC=13. Extract InjectAtrChar(char c). | P2 | OPEN |
| DW-B170-SIM | B170 | SIM GATE: follower stop preserved after leader target drag. | P0 | OPEN (PENDING SIM GATE) |
| DW-B169-K1 | B169 | SIM GATE: 3-stop simultaneous drag | P0 | OPEN |
| DW-B169-K2 | B169 | SIM GATE: single-stop drag regression | P0 | OPEN |
| DW-B168-K1 | B168 | Superseded by DW-B169-K1/K2 | P0 | OPEN (superseded) |
| B167-DEFER-02 | B167 | fo=NULL follower ATM brackets. Blocks DW-B169-K1 Sim102. | P1 | OPEN |
| B167-DEFER-01 | B167 | ECA-1 double-position race. | P2 | OPEN |
| B167-DEFER-03 | B167 | Combined B161-B166 SIM gate | P1 | OPEN |
| B167-DEFER-04 | B167 | B167 SIM gate | P1 | OPEN |
| DW-B159-K2 | B159 | 14 xUnit tests | P2 | OPEN |
| DW-B159-K3 | B159 | Attempt cap hardcoded at 3 | P2 | OPEN |
| DW-B159-K4 | B159 | intendedBePrice sourcing | P2 | OPEN |
