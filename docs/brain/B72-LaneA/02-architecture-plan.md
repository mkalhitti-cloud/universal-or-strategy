# B72-LaneA Architecture Plan

**Status**: RETROSPECTIVE — code already shipped in src/. This document describes what is there and why.
**Block**: B72-LaneA
**Phase**: 1 (Architecture)
**Written by**: ptt-architect
**Source files read**: `docs/standards/jane-street/RULES_CATALOG.md`, `docs/standards/NT8_FULL_REFERENCE.md`,
`src/PropTraderTools/CopyEngine.cs` (2435 lines), `src/PropTraderTools/Features/PttBreakEven.cs` (461 lines),
`docs/brain/B66-LaneC/06-deferred-backlog.md` (reference only — not modified)

---

## 1. Executive Summary

B72-LaneA delivers 22 targeted hotfixes across two files: [`CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs)
and [`PttBreakEven.cs`](src/PropTraderTools/Features/PttBreakEven.cs). All fixes address live-trading
correctness defects observed after blocks B62–B68 shipped.

The dominant themes are:

1. **BE ALL routing** — `ArmAllPendingBe` was calling `SubmitBeStop` directly (skipping followers, bypassing
   the armed-state machine). Fixed to delegate to `ArmPendingBe` per account per position.

2. **`acc.Change()` silent no-op** — NT8's ATM engine owns Stop1/Stop2 bracket orders; `Account.Change()`
   from an AddOn context is a confirmed silent no-op on those orders. The cancel+resubmit pattern is
   the only reliable path and mirrors `PttBreakEven.ExecuteOneAccount`.

3. **OCO ID uniqueness** — `_mstbeOcoSeq` seeded from `Environment.TickCount` and shared between
   `MoveStopToBreakEven` and `PttBreakEven.Execute` via `NextBeOcoSeq()`, preventing OCO ID reuse
   across recompile-within-session cycles.

4. **Instrument reference equality** — NT8 `Instrument` object references are not reliably equal across
   account contexts; all instrument comparisons use `FullName` string equality.

5. **`TryFirePositionState` scope** — fires only on Filled/PartFilled (not Cancelled/Rejected) and only
   for leader accounts (post-Gate-2.5), with a narrow pre-gate exception for follower PTT-BE-Stop fills.

6. **ATM bracket state lifecycle** — `stateOk` filters include all pre-Working states (TriggerPending,
   Submitted, Accepted, Initialized) so that brackets created within the last ~800 ms are caught.

7. **`IsAtmBracketName` generic pattern** — generalized to `Stop[digit]` + `Target[digit]` covering
   Stop1..Stop9 and Target1..Target9 instead of four hardcoded names.

8. **`IsDispatchTriggerState` market/limit dedup** — market orders dispatch on Submitted only; limit
   orders dispatch on Accepted only, preventing double-dispatch caused by NT8/Rithmic OrderId mutation.

**No-pipeline-repairs log**: `docs/brain/NO-PIPELINE-REPAIRS.md` does not exist in this repository.
All repair context is carried in inline code comments in the source files.

**Deferred items from B66-LaneC not closed by B72-LaneA** (OPEN, carry-forward):
- DW-B66-BE-01 — `CancelQxBrackets` cancels PTT-BE-Stop on Quick Exit (P1)
- DW-B66-C-02 — DispatchCopy Gate 5 dedup key = 0.0 for StopLimit (P1)
- DW-B63-01 — Spurious PTT-Copy bracket orders on Sim102 (P1)

---

## 2. Architecture Themes

### Theme 1: BE ALL Path

**Description**

The global break-even path flows:
`OnGlobalBeClick` (panel) → `PttGlobalBreakEven.Execute(bufferTicks)` → `CopyEngine.Instance.ArmAllPendingBe(bufferTicks)` → per-account `ArmPendingBe(pos.Instrument, acc, bufferTicks)` → NT8 `AccountItemUpdate` callback → `OnPendingBeAccountUpdate` → `BreakEven(Account, Instrument, bufferTicks)` → `MoveStopToBreakEven`.

**Before B72-A-01**: `ArmAllPendingBe` was calling `SubmitBeStop` (immediate order submission). This path:
- Skipped follower fan-out (no `AllAccounts()` call)
- Never wrote to `_pendingBeSlots`, so `IsPendingSlotsEmpty()` stayed `true`
- Left the panel in its purple "Armed" visual state permanently

**After B72-A-01**: `ArmAllPendingBe` iterates `Account.All`, skips follower accounts via `IsFollowerAccount`, and for each non-flat position calls `ArmPendingBe` — the same code path as a per-chart BE button click. When `OnPendingBeAccountUpdate` triggers, it calls `BreakEven(Account, Instrument, bufferTicks)` which fans out to followers via `AllAccounts()` → `MoveStopToBreakEven`.

**Immediate-fire sub-path (B72-A-10)**: Inside `ArmPendingBe`, before subscribing `AccountItemUpdate`, if the current bid/ask already satisfies the BE level, `BreakEven()` fires immediately. This covers the "already in the green" case that would have armed a pending watcher that never triggered.

**Rationale**: The pending watcher is a price-level trigger, not an order. If the trigger condition is
already met at arm time, waiting for `AccountItemUpdate` is a race: unrealized P&L updates may lag price.
Immediate fire is the correct and safe path.

---

### Theme 2: `acc.Change()` Silent No-Op

**Description**

`Account.Change()` (NT8_FULL_REFERENCE.md line 328) is the documented API for modifying an existing
order's price or quantity. However, from `AddOnBase` context, calling `acc.Change()` on orders owned
by the NT8 ATM engine (Stop1, Stop2, Target1, Target2) is a **confirmed silent no-op**: no exception
is thrown, the log shows "OK", but the order price does not change at the broker.

**Evidence from code comments** (CopyEngine.cs ~line 1950-1953):
> "Root cause: NT8 ATM engine owns Stop1/Stop2 brackets and ignores acc.Change() from AddOn context —
> no exception, no effect. Confirmed: [MSTBE] Change() OK Stop1 logged while stop remained at original
> price in Orders tab."

**B72-A-12 fix**: `MoveStopToBreakEven` was replaced with the cancel+resubmit pattern:
- Step A: snapshot ATM target orders (while they are still Working/Accepted)
- Step B: cancel all stale brackets on the instrument (the `stateOk` filter — Theme 6)
- Step C: submit new PTT-BE-Stop + PTT-BE-Target-N OCO pairs

This mirrors `PttBreakEven.ExecuteOneAccount` → `CancelStaleBracketsLocal` → `SubmitBeTargetsLocal`,
which has been the authoritative working pattern since B36.

**NT8 API ground truth** (NT8_FULL_REFERENCE.md line 338):
> "`CreateOrder()` — Creates orders for the account that need to be submitted via Submit()"

Both CreateOrder and Submit() are required; CreateOrder alone leaves the order at Initialized state.

---

### Theme 3: OCO ID Uniqueness Strategy

**Description**

Break-even OCO group IDs must be globally unique across:
1. Multiple accounts in the same session
2. Multiple BE presses in the same session (re-entry)
3. Recompile-within-session cycles (NT8 keeps cancelled OCO IDs in memory for the entire session)

**B72-A-13/14 — `_mstbeOcoSeq` TickCount seed**:
```
private volatile int _mstbeOcoSeq = Environment.TickCount;
```
`Environment.TickCount` is milliseconds since OS boot. When NT8 recompiles an AddOn within a running
session, `CopyEngine` is GC'd and re-created. If seeded at 0, the counter restarts at 1 and immediately
collides with pre-recompile OCO IDs still in NT8 memory. `TickCount` advances during recompile so the
post-recompile sequence starts well above any value used in the prior run.

**B72-A-15 — shared `NextBeOcoSeq()` counter**:
```csharp
internal int NextBeOcoSeq() => System.Threading.Interlocked.Increment(ref _mstbeOcoSeq);
```
Both `MoveStopToBreakEven` (CopyEngine.cs) and `PttBreakEven.Execute` (PttBreakEven.cs) call
`CopyEngine.Instance.NextBeOcoSeq()` to obtain their sequence values. Before B72-A-15,
`PttBreakEven` had its own `_beOcoSeq` counter starting at 0. A first press via BE ALL set
`_mstbeOcoSeq=1`; a second press via the per-chart BE button set `_beOcoSeq=1` on a new instance
→ identical OCO ID `"PTT-BE-Sim101-00001-0"` → NT8 OCO reuse error.

**OCO ID format**:
```
"PTT-BE-" + accName[0..7] + "-" + seq.D5 + "-" + pairIndex
```
Example: `"PTT-BE-Sim101-0-00347-0"` (pair 0 of seq 347 on account Sim101-0).

**B72-A-16 — prefix length 4→8** (`PttBreakEven.BuildBeOcoId`):
Old code used `accName.Substring(0, 4)` → `"Sim1"` for both Sim101 AND Sim102, causing per-account
collision. New code uses up to 8 characters of `accName`, giving `"Sim101"` and `"Sim102"` distinct prefixes.

---

### Theme 4: NT8 Instrument Reference Equality

**Description**

NT8 can represent the same tradeable instrument as multiple `Instrument` object instances across
different account contexts. Reference equality (`o.Instrument == instrument`) is unreliable.

**B72-A-08 fix** in `MoveStopToBreakEven`:
- Step A (target snapshot): `o.Instrument.FullName == instrument.FullName` instead of `o.Instrument == instrument`
- Step B (cancel scan): same `FullName` comparison

This mirrors the pre-existing pattern in `FindPosition`, `CancelQxBrackets`, `CancelAllAccountOrders`,
and `SnapshotTargetsPublic`, which all use `FullName` comparison.

**Other affected methods** (pre-existing, confirmed correct by B72):
- `CancelQxBrackets` — `o.Instrument.FullName != instr.FullName`
- `FindPosition` — `p.Instrument.FullName == instrument.FullName`
- `OnOrderUpdate` Gate 2 — `e.Order.Instrument.FullName == rule.Instrument`
- `SubmitBeStop` — `p.Instrument.FullName == instr.FullName`

---

### Theme 5: `TryFirePositionState` Scope

**Description**

`PositionStateChanged` drives the panel's BE button color (Armed/Idle). Incorrect firing causes
false resets. Two fixes narrow the fire conditions:

**B72-A-04 — post-Gate-2.5 placement**:
Before B72-A-04, `TryFirePositionState(e)` was called before Gate 1 (copy-enabled check), so it
fired for ALL order updates on ALL accounts. After B72-A-04, it is called at post-Gate-2.5:
only for orders matching a copy rule's leader account. Follower bracket fills (Stop1/Stop2 fills
when the trade closes) no longer spam `PositionStateChanged` with `hasPos=false`, which was
incorrectly resetting the BE button to Idle.

**B72-A-07 — Cancelled/Rejected removal**:
`TryFirePositionState` fires only on `Filled` and `PartFilled` states. `Cancelled` and `Rejected`
were removed. Rationale: when ATM brackets (Stop1/Stop2/Target1/Target2) cancel as part of closing
a trade, they arrive as a cascade of Cancelled events. None of these alter position quantity.
The position-close signal is correctly delivered via the Filled close event that fires first.

**B72-A-21 — Follower PTT-BE-Stop fill narrow path**:
A follower PTT-BE-Stop fill closes the follower's position, but the post-Gate-2.5 path only covers
the leader. A narrow pre-Gate block was added at the top of `OnOrderUpdate` for exactly this case:
`Filled` + `Name.StartsWith("PTT-BE-Stop")` + account is not a copy-rule master → fire
`PositionStateChanged` so the follower panel resets its BE visual to Idle.

This path does NOT interfere with the Gate chain — it is checked before Gate 1 and returns only
if the narrow condition is matched AND the account is confirmed non-leader.

---

### Theme 6: ATM Bracket State Lifecycle

**Description**

NT8 ATM bracket orders cycle through states before reaching Working:
`TriggerPending` → `Submitted` → `Accepted` → `Working`

For an AddOn to reliably cancel an ATM bracket order, the cancel list must include ALL pre-Working
states. A bracket created within the last ~800 ms may still be in `TriggerPending`, `Submitted`, or
`Accepted` when the next cancel call is issued.

NT8_FULL_REFERENCE.md line 946: "TriggerPending — Order is pending submission."

**B72-A-02 fix** in `CancelQxBrackets`:
```csharp
bool stateOk = o.OrderState == OrderState.Working
            || o.OrderState == OrderState.Initialized
            || o.OrderState == OrderState.Accepted
            || o.OrderState == OrderState.Submitted
            || o.OrderState == OrderState.TriggerPending;
```

**B72-A-03 fix** in `PttBreakEven.CancelStaleBracketsLocal`:
Same five-state `stateOk` set, added `Submitted`, `Accepted`, `TriggerPending`.

**B72-A-11 fix** in `MoveStopToBreakEven` Step B cancel scan:
Same `stateOk` pattern applied to the cancel sweep inside `MoveStopToBreakEven`.

The `notBe` filter (`!o.Name.StartsWith("PTT-BE-", ...)`) ensures that existing PTT-BE-Stop/Target
orders are not cancelled during a re-arm — the new stop replaces old ATM brackets, not itself.

---

### Theme 7: `IsAtmBracketName` Generic Pattern

**Description**

**B72-A-19 fix** in `CopyEngine.IsAtmBracketName`:

Before B72-A-19, the method checked four hardcoded names: `"Stop1"`, `"Stop2"`, `"Target1"`, `"Target2"`.
ATM strategies with 3+ targets (e.g. "MES $200 SL6" with 3 targets) use `Stop3`, `Target3`, etc.
These were not being cancelled by `CancelQxBrackets`, leaving stale bracket orders after BE/QX operations.

After B72-A-19:
```csharp
internal static bool IsAtmBracketName(string name) =>
    !string.IsNullOrEmpty(name) && (
        (name.StartsWith("Stop",   StringComparison.Ordinal) && name.Length > 4 && char.IsDigit(name[4]))
     || (name.StartsWith("Target", StringComparison.Ordinal) && name.Length > 6 && char.IsDigit(name[6]))
    );
```

This covers `Stop1..Stop9` (digit at index 4) and `Target1..Target9` (digit at index 6).

**Edge case noted in code**: `Stop10` → `name[4] == '1'` → `char.IsDigit('1') == true` → returns true.
This is acceptable: NT8 ATM names are Stop1-Stop9 only (single-digit suffix). Stop10 does not occur in
practice but would be correctly caught if it did.

---

### Theme 8: `IsDispatchTriggerState` Market/Limit Dedup

**Description**

**B72-A-22** — `IsDispatchTriggerState(OrderState state, OrderType type)`:

```csharp
internal static bool IsDispatchTriggerState(OrderState state, OrderType type)
    => (type == OrderType.Market && state == OrderState.Submitted)
    || (type == OrderType.Limit  && state == OrderState.Accepted);
```

**Problem**: NT8/Rithmic changes an order's `OrderId` from a GUID (at `Submitted` state) to a numeric
broker-assigned ID (at `Accepted` state). Without type-awareness, both the `Submitted` event and the
`Accepted` event pass the dedup cache with different `OrderId` keys, resulting in two `DispatchCopy`
calls — a double follower entry.

**Market orders**: The GUID `OrderId` is present at `Submitted`. After the state transition to `Accepted`,
the `OrderId` changes to a numeric broker ID. Both events would pass Gate 5 (different keys in
`_dedupCache`). Fix: dispatch on `Submitted` only for market orders.

**Limit orders** (AddOn-created): AddOn limit orders do not produce a `Submitted` event; the first
event arrives as `Accepted`. Fix: dispatch on `Accepted` only for limit orders.

This method is declared `internal static` for direct xUnit testability without NT8 runtime.

---

## 3. Hotfix Catalogue

> B72-A-05 is SUPERSEDED — overwritten by B72-A-06 (HOTFIX-ENTRY-DRAG-DEDUP). Not otherwise documented.

---

### B72-A-01 — `ArmAllPendingBe` Routing Fix

| Field | Value |
|-------|-------|
| File | [`src/PropTraderTools/CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs:617) |
| Method | `ArmAllPendingBe(int bufferTicks)` |
| Change | Replaced direct `SubmitBeStop(acc, pos.Instrument, ...)` call with `ArmPendingBe(pos.Instrument, acc, bufferTicks)` delegation. Added inner position loop per non-follower account. |
| Rationale | `SubmitBeStop` submits immediately without writing to `_pendingBeSlots`; `IsPendingSlotsEmpty()` stayed true; panel never left the purple Armed state. `ArmPendingBe` is the correct armed-state-machine path. |
| JS Rules | JS-021 (no lock — ConcurrentDictionary), JS-002 (void return) |
| Test IDs needed | `T_B72_A_01_ArmAllPendingBe_DelegatesPerAccount`, `T_B72_A_01_ArmAllPendingBe_SkipsFollowers` |

---

### B72-A-02 — `CancelQxBrackets` StateOk Filter

| Field | Value |
|-------|-------|
| File | [`src/PropTraderTools/CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs:507) |
| Method | `CancelQxBrackets(Account acc, Instrument instr)` |
| Change | Added `OrderState.TriggerPending` to `stateOk` filter. |
| Rationale | ATM brackets spend time in `TriggerPending` before `Submitted`. Without this, clicking Quick All immediately after an ATM fill left `TriggerPending` brackets uncancelled — new PTT-QX brackets stacked on top creating double brackets. |
| JS Rules | JS-021 (no lock), JS-002 (void) |
| Test IDs needed | `T_B72_A_02_CancelQxBrackets_IncludesTriggerPending` |

---

### B72-A-03 — `CancelStaleBracketsLocal` StateOk Filter

| Field | Value |
|-------|-------|
| File | [`src/PropTraderTools/Features/PttBreakEven.cs`](src/PropTraderTools/Features/PttBreakEven.cs:171) |
| Method | `CancelStaleBracketsLocal(Account acc, Instrument instr)` |
| Change | Added `OrderState.Submitted`, `OrderState.Accepted`, `OrderState.TriggerPending` to `stateOk` filter. |
| Rationale | Mirrors B72-A-02 fix for `CancelQxBrackets`. BE button pressed quickly after ATM fill must catch brackets in pre-Working states. NT8_FULL_REFERENCE.md line 946 confirms `TriggerPending`. |
| JS Rules | JS-021 (no lock), JS-002 (void) |
| Test IDs needed | `T_B72_A_03_CancelStaleBracketsLocal_IncludesPreWorkingStates` |

---

### B72-A-04 — `OnOrderUpdate` `TryFirePositionState` Placement

| Field | Value |
|-------|-------|
| File | [`src/PropTraderTools/CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs:799) |
| Method | `OnOrderUpdate(object sender, OrderEventArgs e)` |
| Change | Moved `TryFirePositionState(e)` from before Gate 1 to after Gate 2.5. |
| Rationale | Before this fix, follower bracket fills (Stop1/Stop2 cancels when position closes) fired `PositionStateChanged(hasPos=false)` and incorrectly reset the BE button to Idle on every bracket cancel event. Post-Gate-2.5 placement scopes fires to leader account+instrument orders only. |
| JS Rules | JS-021 (no lock), JS-002 (void) |
| Test IDs needed | `T_B72_A_04_TryFirePositionState_PostGate25_LeaderOnly` |

---

### B72-A-06 — `HandleEntryChange` Dedup Upsert

| Field | Value |
|-------|-------|
| File | [`src/PropTraderTools/CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs:1221) |
| Method | `HandleEntryChange(Order leaderOrder, CopyRule rule)` |
| Change | Changed `_dedupCache.TryRemove(orderId)` to `_dedupCache[orderId] = newPrice` (upsert, not remove). |
| Rationale | `TryRemove` caused the Working-state re-entry event to find no cache entry and fall through Gate C into `DispatchCopy`, placing a second PTT-Copy order (doubling follower contracts). Keeping the key at `newPrice` means the subsequent Working event sees delta=0 and returns without dispatching. |
| JS Rules | JS-025 (ConcurrentDictionary lock-free), JS-021 (no lock) |
| Test IDs needed | `T_B72_A_06_HandleEntryChange_UpsertNotRemove_NoDoubleDispatch` |

---

### B72-A-07 — `TryFirePositionState` Cancelled/Rejected Removal

| Field | Value |
|-------|-------|
| File | [`src/PropTraderTools/CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs:1289) |
| Method | `TryFirePositionState(OrderEventArgs e)` |
| Change | Removed `Cancelled` and `Rejected` from the state filter. Now fires only on `Filled` and `PartFilled`. |
| Rationale | ATM bracket cancels (Stop1/Stop2/Target1/Target2 all cancel when the position closes) fire `PositionStateChanged(hasPos=false)` hundreds of times. Only `Filled` and `PartFilled` events can open or grow a position. The flat signal is correctly delivered by the Filled close event. |
| JS Rules | JS-002 (no null return), JS-021 (no lock) |
| Test IDs needed | `T_B72_A_07_TryFirePositionState_OnlyFilledPartFilled`, `T_B72_A_07_TryFirePositionState_CancelledDoesNotFire` |

---

### B72-A-08 — `MoveStopToBreakEven` Instrument FullName

| Field | Value |
|-------|-------|
| File | [`src/PropTraderTools/CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs:1961) |
| Method | `MoveStopToBreakEven(Account acc, Instrument instrument, int bufferTicks)` |
| Change | In Step A (target snapshot) and Step B (cancel scan): changed `o.Instrument == instrument` to `o.Instrument != null && o.Instrument.FullName == instrument.FullName`. |
| Rationale | NT8 can represent the same tradeable as multiple `Instrument` instances across account contexts. Reference equality fails silently, causing target snapshots to return 0 and cancel scans to skip orders. `FullName` string comparison is the authoritative pattern used throughout the codebase. |
| JS Rules | JS-021 (no lock), JS-002 (void) |
| Test IDs needed | `T_B72_A_08_MoveStopToBreakEven_UsesFullNameComparison` |

---

### B72-A-09 — `MoveStopToBreakEven` Direction Sign

| Field | Value |
|-------|-------|
| File | [`src/PropTraderTools/CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs:1975) |
| Method | `MoveStopToBreakEven(Account acc, Instrument instrument, int bufferTicks)` |
| Change | `double direction = isLong ? -1.0 : +1.0;` (was `isLong ? +1.0 : -1.0`). |
| Rationale | Long stop must go BELOW entry (entry - buf*tick) to fire when price drops back; short stop must go ABOVE entry (entry + buf*tick) to fire when price rises back. The old sign was inverted: short positions had their stop placed below entry — immediately triggerable as a market order. This fix aligns with the sign correction in `PttBreakEven.ExecuteOneAccount` (B72-A-17). |
| JS Rules | JS-002 (void), JS-021 (no lock) |
| Test IDs needed | `T_B72_A_09_MoveStopToBreakEven_LongStopBelowEntry`, `T_B72_A_09_MoveStopToBreakEven_ShortStopAboveEntry` |

---

### B72-A-10 — `ArmPendingBe` Immediate-Fire

| Field | Value |
|-------|-------|
| File | [`src/PropTraderTools/CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs:2267) |
| Method | `ArmPendingBe(Instrument instr, Account masterAcc, int bufferTicks)` |
| Change | Added immediate-fire check: if bid/ask already satisfies the BE trigger level, call `BreakEven()` directly and return without arming the watcher. |
| Rationale | If the position is already "in the green" (bid >= entry for long; ask <= entry for short), the `AccountItemUpdate` watcher may never trigger (UPnL may lag price). Mirrors `TradeCopierPanel.OnBeClick`'s `IsPriceAlreadyAtBe` check for the per-chart path. Covers the "BE ALL on already-profitable positions" case. |
| JS Rules | JS-021 (no lock — ConcurrentDictionary indexer), JS-002 (void) |
| Test IDs needed | `T_B72_A_10_ArmPendingBe_ImmediateFire_WhenAlreadyAtBe`, `T_B72_A_10_ArmPendingBe_Arms_WhenNotAtBe` |

---

### B72-A-11 — `MoveStopToBreakEven` StateOk Filter

| Field | Value |
|-------|-------|
| File | [`src/PropTraderTools/CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs:2017) |
| Method | `MoveStopToBreakEven(Account acc, Instrument instrument, int bufferTicks)` — Step B |
| Change | Added `OrderState.Initialized`, `OrderState.Submitted`, `OrderState.TriggerPending` to Step B cancel scan `stateOk` filter. |
| Rationale | Aligns Step B's cancel sweep with the stateOk filter in `CancelQxBrackets` and `CancelStaleBracketsLocal`. Without `TriggerPending`, brackets created <800 ms before the BE press are missed and remain live alongside the new PTT-BE-Stop orders. |
| JS Rules | JS-021 (no lock), JS-002 (void) |
| Test IDs needed | `T_B72_A_11_MoveStopToBreakEven_StepB_IncludesPreWorkingStates` |

---

### B72-A-12 — `MoveStopToBreakEven` Cancel+Resubmit

| Field | Value |
|-------|-------|
| File | [`src/PropTraderTools/CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs:1949) |
| Method | `MoveStopToBreakEven(Account acc, Instrument instrument, int bufferTicks)` |
| Change | Replaced `acc.Change(new Order[]{stopOrder})` with full cancel+resubmit: Step A snapshot targets, Step B cancel all stale brackets, Step C submit new PTT-BE-Stop+Target OCO pairs. |
| Rationale | `acc.Change()` is a confirmed silent no-op on ATM-owned brackets from AddOn context. The cancel+resubmit pattern (identical to `PttBreakEven.ExecuteOneAccount`) is the only reliable way to move the stop. NT8_FULL_REFERENCE.md confirms `CreateOrder` + `Submit()` is required. |
| JS Rules | JS-001 (try/catch per order — no throw), JS-021 (no lock), JS-002 (void), JS-033 (synchronous) |
| Test IDs needed | `T_B72_A_12_MoveStopToBreakEven_CancelResubmit_NotChange` |

---

### B72-A-13 / B72-A-14 — `_mstbeOcoSeq` TickCount Seed + D5 Format

| Field | Value |
|-------|-------|
| File | [`src/PropTraderTools/CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs:165) |
| Field | `private volatile int _mstbeOcoSeq = Environment.TickCount;` |
| Change | Changed seed from `0` to `Environment.TickCount`. D5 format (`seq.ToString("D5")`) for OCO ID sequence component. |
| Rationale | NT8 retains cancelled OCO IDs for the entire session. On AddOn recompile-within-session, `CopyEngine` is GC'd and re-created. A seed of 0 restarts at 1, immediately colliding with pre-recompile OCO IDs. `Environment.TickCount` (ms since OS boot) advances during recompile, so the post-recompile counter starts far above prior values. D5 format pads to 5 digits for consistent string length. |
| JS Rules | JS-023 (volatile int allowed), JS-021 (no lock — Interlocked.Increment) |
| Test IDs needed | `T_B72_A_14_OcoSeq_TickCountSeed_NonZero` |

---

### B72-A-15 — `NextBeOcoSeq` Shared Counter

| Field | Value |
|-------|-------|
| Files | [`src/PropTraderTools/CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs:166), [`src/PropTraderTools/Features/PttBreakEven.cs`](src/PropTraderTools/Features/PttBreakEven.cs:66) |
| Methods | `CopyEngine.NextBeOcoSeq()`, `PttBreakEven.Execute()` |
| Change | Added `internal int NextBeOcoSeq()` on `CopyEngine` (Interlocked.Increment on `_mstbeOcoSeq`). Removed per-instance `_beOcoSeq` from `PttBreakEven`; `Execute()` now calls `CopyEngine.Instance.NextBeOcoSeq()`. |
| Rationale | Per-instance `_beOcoSeq` in `PttBreakEven` started at 0 on each new instance. A first BE ALL press set `_mstbeOcoSeq=1`; a second per-chart BE press set `_beOcoSeq=1` on a new instance → identical OCO ID → NT8 OCO reuse error. Sharing one counter via `CopyEngine.Instance` guarantees global uniqueness. |
| JS Rules | JS-023 (Interlocked.Increment — lock-free), JS-021 (no lock) |
| Test IDs needed | `T_B72_A_15_NextBeOcoSeq_Shared_NoDuplicates`, `T_B72_A_15_PttBreakEven_UsesSharedCounter` |

---

### B72-A-16 — `BuildBeOcoId` Prefix 4→8

| Field | Value |
|-------|-------|
| File | [`src/PropTraderTools/Features/PttBreakEven.cs`](src/PropTraderTools/Features/PttBreakEven.cs:342) |
| Method | `BuildBeOcoId(string accName, int seq, int pairIndex)` |
| Change | Changed `accName.Substring(0, 4)` to `accName.Length >= 8 ? accName.Substring(0, 8) : accName`. |
| Rationale | "Sim101" and "Sim102" both produce `"Sim1"` with a 4-char prefix → identical OCO ID across accounts → NT8 OCO collision. 8 chars gives `"Sim101"` and `"Sim102"`, which are distinct. |
| JS Rules | JS-002 (string concat never null), JS-021 (no lock — pure computation) |
| Test IDs needed | `T_B72_A_16_BuildBeOcoId_8CharPrefix_Sim101VsSim102` |

---

### B72-A-17 — `ExecuteOneAccount` bePrice Sign

| Field | Value |
|-------|-------|
| File | [`src/PropTraderTools/Features/PttBreakEven.cs`](src/PropTraderTools/Features/PttBreakEven.cs:99) |
| Method | `ExecuteOneAccount(Account acc, IPttHostContext ctx, double buf, double tickSize, int seq)` |
| Change | `double bePrice = pos.AveragePrice + (isLong ? -buf : +buf) * tickSize;` (was `isLong ? +buf : -buf`). |
| Rationale | Same sign inversion as B72-A-09 for `MoveStopToBreakEven`. Long stop must be below entry; short stop must be above entry. Old sign placed short stop below entry — immediately triggerable. |
| JS Rules | JS-002 (void), JS-021 (no lock) |
| Test IDs needed | `T_B72_A_17_ExecuteOneAccount_LongBePriceBelowEntry`, `T_B72_A_17_ExecuteOneAccount_ShortBePriceAboveEntry` |

---

### B72-A-18 — `RaiseBeNotify` Sign

| Field | Value |
|-------|-------|
| File | [`src/PropTraderTools/Features/PttBreakEven.cs`](src/PropTraderTools/Features/PttBreakEven.cs:150) |
| Method | `RaiseBeNotify(IPttHostContext ctx, Position leaderPos, double buf, double tickSize)` |
| Change | `double leaderBePrice = leaderPos.AveragePrice + (leaderIsLong ? -buf : +buf) * tickSize;` (was `isLong ? +buf : -buf`). |
| Rationale | `RaiseBeNotify` must report the same BE price as `ExecuteOneAccount` to keep PttBus consumers (e.g. panel display) consistent with the actual submitted stop price. Aligned with B72-A-17. |
| JS Rules | JS-002 (void), JS-021 (no lock) |
| Test IDs needed | `T_B72_A_18_RaiseBeNotify_LongBePriceBelowEntry` |

---

### B72-A-19 — `IsAtmBracketName` Generic Pattern

| Field | Value |
|-------|-------|
| File | [`src/PropTraderTools/CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs:478) |
| Method | `IsAtmBracketName(string name)` |
| Change | Replaced hardcoded 4-name check (`"Stop1"`, `"Stop2"`, `"Target1"`, `"Target2"`) with generic `Stop[digit]` + `Target[digit]` pattern. |
| Rationale | ATM strategies with 3+ targets (Stop3, Target3, etc.) were not being cancelled. Generic digit check covers Stop1..Stop9 and Target1..Target9. Stop10 would also return true (acceptable: NT8 ATM uses single-digit suffixes only). |
| JS Rules | JS-002 (bool — never null), JS-021 (no lock — pure static method) |
| Test IDs needed | `T_B72_A_19_IsAtmBracketName_Stop1_True`, `T_B72_A_19_IsAtmBracketName_Stop3_True`, `T_B72_A_19_IsAtmBracketName_Target9_True`, `T_B72_A_19_IsAtmBracketName_PTT_False` |

---

### B72-A-20 — `CancelStaleBracketsLocal` notBe Filter

| Field | Value |
|-------|-------|
| File | [`src/PropTraderTools/Features/PttBreakEven.cs`](src/PropTraderTools/Features/PttBreakEven.cs:186) |
| Method | `CancelStaleBracketsLocal(Account acc, Instrument instr)` |
| Change | Changed `notBe` from exact-match check to `!o.Name.StartsWith("PTT-BE-", StringComparison.Ordinal)`. |
| Rationale | The exact-match list only excluded `"PTT-BE-Stop"`. After OCO pairs were introduced (B36), orders named `"PTT-BE-Stop-1"`, `"PTT-BE-Stop-2"`, `"PTT-BE-Target-1"`, etc. were incorrectly being cancelled during a re-arm. The prefix guard excludes the entire PTT-BE-* family. |
| JS Rules | JS-002 (void), JS-021 (no lock) |
| Test IDs needed | `T_B72_A_20_CancelStaleBracketsLocal_ExcludesPttBePrefix` |

---

### B72-A-21 — `OnOrderUpdate` Follower Flat Disarm

| Field | Value |
|-------|-------|
| File | [`src/PropTraderTools/CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs:754) |
| Method | `OnOrderUpdate(object sender, OrderEventArgs e)` — narrow pre-Gate block |
| Change | Added pre-Gate-1 block: if the order is `Filled` + `Name.StartsWith("PTT-BE-Stop")` + account is not a copy-rule master → fire `PositionStateChanged` with current position state. |
| Rationale | Gate 2 passes only leader orders. Follower PTT-BE-Stop fills (when the follower's stop fires and closes the position) never reached `TryFirePositionState`. The panel's `_beState` stayed Armed after the follower BE executed. This narrow path re-enables disarm for the follower side without affecting the existing leader path. |
| JS Rules | JS-021 (no lock), JS-002 (void) |
| Test IDs needed | `T_B72_A_21_FollowerBeStopFill_FiresPositionStateChanged`, `T_B72_A_21_LeaderBeStopFill_UsesNormalPath` |

---

### B72-A-22 — `IsDispatchTriggerState` Market/Limit

| Field | Value |
|-------|-------|
| File | [`src/PropTraderTools/CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs:922) |
| Method | `IsDispatchTriggerState(OrderState state, OrderType type)` |
| Change | New `internal static` method: market orders dispatch on `Submitted` only; limit orders dispatch on `Accepted` only. Used at Gate 3 in `DispatchCopy`. |
| Rationale | NT8/Rithmic changes `OrderId` from GUID (at Submitted) to numeric broker ID (at Accepted) for market orders. Both events passed the dedup cache with different keys → double dispatch. AddOn limit orders skip Submitted and arrive first as Accepted. Type-aware dispatch gate prevents double-fire. |
| JS Rules | JS-002 (bool — never null), JS-021 (no lock — pure static) |
| Test IDs needed | `T_B72_A_22_IsDispatchTriggerState_Market_Submitted_True`, `T_B72_A_22_IsDispatchTriggerState_Market_Accepted_False`, `T_B72_A_22_IsDispatchTriggerState_Limit_Accepted_True`, `T_B72_A_22_IsDispatchTriggerState_Limit_Submitted_False` |

---

### B72-A-23 — `MoveStopToBreakEven` Step A `isAtmTarget`

| Field | Value |
|-------|-------|
| File | [`src/PropTraderTools/CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs:1995) |
| Method | `MoveStopToBreakEven(Account acc, Instrument instrument, int bufferTicks)` — Step A |
| Change | Widened `isAtmTarget` check to also match PTT-QX-T* names and PTT-BE-Target-* names (previously only ATM Target1..Target9). |
| Rationale | After a Quick Exit (QX) operation, ATM targets are replaced by `PTT-QX-T1`/`PTT-QX-T2`. Without this widening, `MoveStopToBreakEven` Step A always found 0 targets after QX and took the bare-stop (no-target) path — omitting the OCO structure. Including `PTT-QX-T*` and `PTT-BE-Target-*` enables correct OCO pair generation after QX and after a prior BE arm. |
| JS Rules | JS-002 (bool — no null from `string.IsNullOrEmpty` guard), JS-021 (no lock) |
| Test IDs needed | `T_B72_A_23_MoveStopToBreakEven_StepA_IncludesPttQxTargets`, `T_B72_A_23_MoveStopToBreakEven_StepA_IncludesPttBeTargets` |

---

## 4. Cross-Cutting Concerns

### JS Rule Constraints

All B72-LaneA hotfix code is subject to the following mandatory JS rules:

| Rule | Category | Applies To |
|------|----------|------------|
| JS-001 | Type Safety | All `CreateOrder` calls wrapped in `try/catch`; no throw in hot paths |
| JS-002 | Type Safety | No `return null`; void methods throughout; bool predicates return `false` as default |
| JS-021 | Concurrency | `lock()` banned; all concurrent state via `ConcurrentDictionary`, `volatile`, `Interlocked` |
| JS-023 | Concurrency | `volatile int _mstbeOcoSeq`; `Interlocked.Increment` in `NextBeOcoSeq()` |
| JS-025 | Performance | `ConcurrentDictionary` for `_pendingBeSlots`, `_dedupCache` — lock-free operations |
| JS-033 | Concurrency | No `async void`; all BE methods are synchronous void |
| JS-036/037 | Performance | No heap allocation in hot paths; `List<Order>` for cancel batches is transient and acceptable at this granularity |

**P0 violations confirmed absent** in B72 hotfix code:
- No `lock()` keywords
- No `async void` (non-event-handler)
- No `return null` for missing values (void returns throughout)
- No `throw` in hot paths

**Pre-existing non-ASCII** (noted in B66-LaneC/06-deferred-backlog.md, PRE-EXISTING-01/02):
- Em-dash characters at CopyEngine.cs lines ~398, ~499 (comment-only, not in B72 regions)
- Unicode arrow characters at ~1449-1450 (comment-only, not in B72 regions)
These are not introduced by B72-LaneA.

### xUnit Test Framework Mandate

All tests must use xUnit `[Fact]` attributes. MSTest and NUnit are banned
(docs/protocol/TEST_FRAMEWORK_PROTOCOL.md).

All `internal static` predicates (`IsDispatchTriggerState`, `IsAtmBracketName`, `IsAtmTargetName`,
`IsPttQxTarget`) are designed for direct xUnit testability without NT8 runtime dependencies.

### NT8 API Constraints

| NT8 Constraint | Applied In |
|----------------|-----------|
| `CreateOrder()` requires explicit `Submit()` — order stays at `Initialized` otherwise | All BE stop/target submissions in `MoveStopToBreakEven`, `SubmitBeStopLocal`, `SubmitBeTargetsLocal` |
| `acc.Change()` is a silent no-op on ATM-bracket-owned orders from AddOn context | B72-A-12: replaced with cancel+resubmit throughout |
| `OrderState.TriggerPending` is a valid pre-Working state | B72-A-02, A-03, A-11: added to `stateOk` filters |
| `Instrument` reference equality unreliable across account contexts | B72-A-08: `FullName` string comparison enforced |
| NT8/Rithmic changes `OrderId` from GUID to numeric at `Accepted` for market orders | B72-A-22: `IsDispatchTriggerState` type-aware gate |
| `AtmStrategyCreate()` is `StrategyBase`-only — not available in `AddOnBase` | Not applicable to B72 hotfixes; noted in DW-B54-01 (OPEN, blocked) |
| StopMarket arg6=limitPrice=0, arg7=stopPrice — NEVER swap | Enforced in all `CreateOrder` calls for stop orders |
| Limit arg6=limitPrice, arg7=stopPrice=0 | Enforced in all `CreateOrder` calls for limit orders |
| `DateTime.MaxValue` for GTC orders — NOT `DateTime.Now` | All `CreateOrder` calls use `DateTime.MaxValue` |
| Signal names must start with `"PTT-"` | `"PTT-BE-Stop"`, `"PTT-BE-Stop-N"`, `"PTT-BE-Target-N"` |
| arg11 = `(NinjaTrader.Cbi.CustomOrder)null` — NOT a string | All 12-arg `CreateOrder` calls |

---

## 5. Files Modified

| File | Hotfix IDs | Net Change Summary |
|------|------------|--------------------|
| [`src/PropTraderTools/CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs) | A-01, A-02, A-04, A-06, A-07, A-08, A-09, A-10, A-11, A-12, A-13/14, A-15, A-19, A-21, A-22, A-23 | `ArmAllPendingBe` rerouted; `CancelQxBrackets` stateOk widened; `OnOrderUpdate` `TryFirePositionState` relocated + follower path added; `HandleEntryChange` upsert; `TryFirePositionState` state filter narrowed; `MoveStopToBreakEven` full rewrite to cancel+resubmit with correct sign, FullName, stateOk, and widened isAtmTarget; `_mstbeOcoSeq` TickCount seed; `NextBeOcoSeq()` added; `IsAtmBracketName` generalized; `IsDispatchTriggerState` added |
| [`src/PropTraderTools/Features/PttBreakEven.cs`](src/PropTraderTools/Features/PttBreakEven.cs) | A-03, A-15, A-16, A-17, A-18, A-20 | `CancelStaleBracketsLocal` stateOk widened + notBe prefix guard; `_beOcoSeq` removed; `Execute()` uses `CopyEngine.Instance.NextBeOcoSeq()`; `BuildBeOcoId` prefix 8 chars; `ExecuteOneAccount` + `RaiseBeNotify` sign corrected |

**Files NOT modified** by B72-LaneA:
- `src/PropTraderTools/TradeCopierPanel.cs`
- `src/PropTraderTools/Features/PttGlobalBreakEven.cs`
- `src/PropTraderTools/Features/PttGlobalQuickExit.cs`
- `src/PropTraderTools/Features/PttQuickExit.cs`

---

## 6. Requirements Traceability

| Hotfix ID | Theme | Root Defect | Fix Pattern | Closed Deferred Item |
|-----------|-------|------------|-------------|----------------------|
| B72-A-01 | Theme 1 | BE ALL never reached armed state | Delegate to `ArmPendingBe` | — |
| B72-A-02 | Theme 6 | Double brackets after QX + ATM fill | `stateOk` includes `TriggerPending` | — |
| B72-A-03 | Theme 6 | BE on fresh ATM fill missed pre-Working orders | `stateOk` widened in PttBreakEven | — |
| B72-A-04 | Theme 5 | Follower bracket fills reset BE button | `TryFirePositionState` post-Gate-2.5 | — |
| B72-A-06 | — | Double PTT-Copy on dragged entry | Upsert not remove in `HandleEntryChange` | — |
| B72-A-07 | Theme 5 | ATM bracket cancel cascade reset BE button | Fire only on Filled/PartFilled | — |
| B72-A-08 | Theme 4 | `MoveStopToBreakEven` missed orders (ref eq) | `FullName` comparison | — |
| B72-A-09 | — | Short BE stop placed below entry | Sign inversion: `isLong ? -1 : +1` | — |
| B72-A-10 | Theme 1 | BE ALL on profitable position never triggered | Immediate-fire path in `ArmPendingBe` | — |
| B72-A-11 | Theme 6 | `MoveStopToBreakEven` Step B missed pre-Working | `stateOk` widened | — |
| B72-A-12 | Theme 2 | `acc.Change()` silent no-op on ATM brackets | Cancel+resubmit pattern | — |
| B72-A-13/14 | Theme 3 | OCO ID collision on recompile-within-session | `Environment.TickCount` seed, D5 format | — |
| B72-A-15 | Theme 3 | Per-instance OCO counter collision | Shared `NextBeOcoSeq()` on `CopyEngine` | — |
| B72-A-16 | Theme 3 | OCO ID collision across Sim101/Sim102 | 8-char account prefix in `BuildBeOcoId` | — |
| B72-A-17 | — | Short BE stop placed below entry (PttBreakEven) | Sign inversion in `ExecuteOneAccount` | — |
| B72-A-18 | — | PttBus BE event reported wrong price for short | Sign aligned in `RaiseBeNotify` | — |
| B72-A-19 | Theme 7 | ATM strategies with 3+ targets not cancelled | Generic `Stop[d]`/`Target[d]` predicate | — |
| B72-A-20 | — | `CancelStaleBracketsLocal` cancelled PTT-BE-Stop-N | Prefix guard instead of exact match | — |
| B72-A-21 | Theme 5 | Follower BE button stuck Armed after fill | Narrow pre-Gate path for follower fills | — |
| B72-A-22 | Theme 8 | Double follower market order on NT8/Rithmic | `IsDispatchTriggerState` type-aware | — |
| B72-A-23 | Theme 1 | BE ALL after QX took bare-stop path (no OCO) | Widened `isAtmTarget` to include QX targets | — |

**Carry-forward OPEN deferred items** (unchanged by B72-LaneA):

| ID | Description | Priority | Status |
|----|-------------|----------|--------|
| DW-B66-BE-01 | `CancelQxBrackets` cancels PTT-BE-Stop on Quick Exit | P1 | OPEN |
| DW-B66-C-02 | DispatchCopy Gate 5 dedup key = 0.0 for StopLimit | P1 | OPEN |
| DW-B63-01 | Spurious PTT-Copy brackets on Sim102 after ATM fill | P1 | OPEN |
| DW-B54-01 | ATM auto-inject (blocked — StrategyBase required) | P1 | OPEN (blocked) |
| DW-B58-01 | `SnapshotTargetsPublic` hardcoded prefixes | P2 | OPEN |
| DW-B58-02 | `GlobalBe` non-atomic lazy init | P2 | OPEN |
| DW-B58-03 | `RelayBe` OcoGroup not forwarded | P2 | OPEN |
| PRE-EXISTING-01 | Non-ASCII em-dash CopyEngine.cs lines ~398, ~499 | P2 | OPEN |
| PRE-EXISTING-02 | Non-ASCII arrows CopyEngine.cs lines ~1449-1450 | P2 | OPEN |
| PRE-EXISTING-03 | deploy-sync.ps1 archived; PropTraderTools sync is manual | P2 | OPEN |

---

## 7. Test ID Mapping Table

Each row maps a canonical spec test ID to the hotfix it exercises, the source file, the method under test, and what the test asserts.

| Test ID | Hotfix ID | File | Method | What it tests |
|---------|-----------|------|--------|---------------|
| T_BEALL_01 | B72-A-01 | CopyEngine.cs | ArmAllPendingBe | 1 non-follower open account -> `_pendingBeSlots` populated |
| T_BEALL_02 | B72-A-01 | CopyEngine.cs | ArmAllPendingBe | 2 non-follower accounts -> both slots populated |
| T_BEALL_03 | B72-A-01 | CopyEngine.cs | ArmAllPendingBe | Follower account in Account.All -> skipped, not armed |
| T_BEALL_04 | B72-A-01 | CopyEngine.cs | ArmAllPendingBe | Flat position (Quantity=0) -> skipped, not armed |
| T_QX_DOUBLE_01 | B72-A-02 | CopyEngine.cs | CancelQxBrackets | TriggerPending bracket on instrument -> cancelled |
| T_QX_DOUBLE_02 | B72-A-02 | CopyEngine.cs | CancelQxBrackets | Working bracket on instrument -> cancelled |
| T_QX_DOUBLE_03 | B72-A-02 | CopyEngine.cs | CancelQxBrackets | Submitted + Accepted brackets -> both cancelled |
| T_BE_CANCEL_01 | B72-A-03 | PttBreakEven.cs | CancelStaleBracketsLocal | TriggerPending order IS in stale list -> cancelled |
| T_BE_CANCEL_02 | B72-A-03 | PttBreakEven.cs | CancelStaleBracketsLocal | Submitted order IS in stale list -> cancelled |
| T_BE_CANCEL_03 | B72-A-03 | PttBreakEven.cs | CancelStaleBracketsLocal | Accepted order IS in stale list -> cancelled |
| T_BE_RESET_01 | B72-A-04 | CopyEngine.cs | OnOrderUpdate | Follower Stop1 fill before Gate 2.5 -> does NOT fire PositionStateChanged |
| T_BE_RESET_02 | B72-A-04 | CopyEngine.cs | OnOrderUpdate | Leader Filled event post-Gate-2.5 -> DOES fire TryFirePositionState |
| T_DRAG_DEDUP_02 | B72-A-06 | CopyEngine.cs | HandleEntryChange | Cache entry upserted (not removed) -> subsequent Working event sees delta=0, no dispatch |
| T_DRAG_DEDUP_03 | B72-A-06 | CopyEngine.cs | HandleEntryChange | First Working event with new price -> dispatch proceeds (cache miss on new price) |
| T_DRAG_DEDUP_04 | B72-A-06 | CopyEngine.cs | HandleEntryChange | TryRemove path removed -> no second DispatchCopy on same order |
| T_TRYFIRE_01 | B72-A-07 | CopyEngine.cs | TryFirePositionState | Filled state -> fires PositionStateChanged |
| T_TRYFIRE_02 | B72-A-07 | CopyEngine.cs | TryFirePositionState | Cancelled state -> does NOT fire PositionStateChanged |
| T_TRYFIRE_03 | B72-A-07 | CopyEngine.cs | TryFirePositionState | Rejected state -> does NOT fire PositionStateChanged |
| T_BE_MOVE_01 | B72-A-08 | CopyEngine.cs | MoveStopToBreakEven | Instrument with same FullName but different reference -> matched correctly (FullName eq) |
| T_BE_MOVE_02 | B72-A-08 | CopyEngine.cs | MoveStopToBreakEven | Two distinct instruments with different FullNames -> filtered out |
| T_BE_MOVE_03 | B72-A-12 | CopyEngine.cs | MoveStopToBreakEven | Cancel+resubmit path executes (no acc.Change() call) |
| T_BE_MOVE_04 | B72-A-11 | CopyEngine.cs | MoveStopToBreakEven | Step B cancel scan includes TriggerPending state |
| T_BE_MOVE_05 | B72-A-23 | CopyEngine.cs | MoveStopToBreakEven | Step A includes PTT-QX-T* targets in isAtmTarget check |
| T_BE_SIGN_LONG_01 | B72-A-09 | CopyEngine.cs | MoveStopToBreakEven | Long position -> bePrice = entry - bufferTicks * tickSize (below entry) |
| T_BE_SIGN_SHORT_01 | B72-A-09 | CopyEngine.cs | MoveStopToBreakEven | Short position -> bePrice = entry + bufferTicks * tickSize (above entry) |
| T_BE_SIGN_ZERO | B72-A-09 | CopyEngine.cs | MoveStopToBreakEven | bufferTicks=0 -> bePrice = entry exactly |
| T_BE_IMM_01 | B72-A-10 | CopyEngine.cs | ArmPendingBe | Long: bid >= entry -> immediate BreakEven() call, no watcher armed |
| T_BE_IMM_02 | B72-A-10 | CopyEngine.cs | ArmPendingBe | Short: ask <= entry -> immediate BreakEven() call, no watcher armed |
| T_BE_IMM_03 | B72-A-10 | CopyEngine.cs | ArmPendingBe | Long: bid < entry -> watcher armed (AccountItemUpdate subscribed) |
| T_BE_IMM_04 | B72-A-10 | CopyEngine.cs | ArmPendingBe | Short: ask > entry -> watcher armed (AccountItemUpdate subscribed) |
| T_MSTBE_CR_01 | B72-A-12 | CopyEngine.cs | MoveStopToBreakEven | Step A snapshots ATM Target1..Target9 orders before cancel |
| T_MSTBE_CR_02 | B72-A-12 | CopyEngine.cs | MoveStopToBreakEven | Step B cancels all stale brackets (Working + pre-Working states) |
| T_MSTBE_CR_03 | B72-A-12 | CopyEngine.cs | MoveStopToBreakEven | Step C submits PTT-BE-Stop + PTT-BE-Target-N OCO pairs |
| T_OCO_SEED_01 | B72-A-13/14 | CopyEngine.cs | _mstbeOcoSeq field | Initial value of _mstbeOcoSeq is Environment.TickCount (non-zero) |
| T_OCO_SEED_02 | B72-A-13/14 | CopyEngine.cs | _mstbeOcoSeq field | After simulated recompile (new CopyEngine instance), seed does not restart at 1 |
| T_OCO_SEED_03 | B72-A-14 | CopyEngine.cs | NextBeOcoSeq | OCO sequence uses D5 format (5-digit zero-padded string) |
| T_OCO_SEQ_01 | B72-A-15 | CopyEngine.cs / PttBreakEven.cs | NextBeOcoSeq / Execute | First call from MoveStopToBreakEven and first call from PttBreakEven.Execute return different sequence values |
| T_OCO_SEQ_04 | B72-A-15 | CopyEngine.cs | NextBeOcoSeq | Concurrent calls to NextBeOcoSeq return strictly unique values (Interlocked.Increment) |
| T_OCO_SHARED_01 | B72-A-15 | PttBreakEven.cs | Execute | PttBreakEven.Execute calls CopyEngine.Instance.NextBeOcoSeq() (shared counter) |
| T_OCO_SHARED_02 | B72-A-15 | CopyEngine.cs | NextBeOcoSeq | No per-instance _beOcoSeq exists in PttBreakEven after B72-A-15 |
| T_OCO_ID_01 | B72-A-16 | PttBreakEven.cs | BuildBeOcoId | accName "Sim101" -> prefix "Sim101" (8 chars or full if shorter) |
| T_OCO_ID_02 | B72-A-16 | PttBreakEven.cs | BuildBeOcoId | accName "Sim102" -> prefix "Sim102", distinct from Sim101 prefix |
| T_OCO_ID_03 | B72-A-16 | PttBreakEven.cs | BuildBeOcoId | accName shorter than 8 chars -> full name used as prefix |
| T_BE_PRICE_LONG_01 | B72-A-17 | PttBreakEven.cs | ExecuteOneAccount | Long position: bePrice = avgPrice - buf * tickSize (below entry) |
| T_BE_PRICE_LONG_02 | B72-A-17 | PttBreakEven.cs | ExecuteOneAccount | Long: buf=0 -> bePrice = avgPrice exactly |
| T_BE_PRICE_SHORT_01 | B72-A-17 | PttBreakEven.cs | ExecuteOneAccount | Short position: bePrice = avgPrice + buf * tickSize (above entry) |
| T_BE_PRICE_SHORT_02 | B72-A-17 | PttBreakEven.cs | ExecuteOneAccount | Short: buf=2, tickSize=0.25 -> bePrice = avgPrice + 0.50 |
| T_BE_PRICE_VALID_SHORT | B72-A-17 | PttBreakEven.cs | ExecuteOneAccount | Short bePrice is strictly above avgPrice when buf > 0 |
| T_NOTIFY_01 | B72-A-18 | PttBreakEven.cs | RaiseBeNotify | Long: reported leaderBePrice = avgPrice - buf * tickSize |
| T_NOTIFY_02 | B72-A-18 | PttBreakEven.cs | RaiseBeNotify | Short: reported leaderBePrice = avgPrice + buf * tickSize |
| T_ATM_T3_01 | B72-A-19 | CopyEngine.cs | IsAtmBracketName | "Stop1" -> true |
| T_ATM_T3_02 | B72-A-19 | CopyEngine.cs | IsAtmBracketName | "Stop3" -> true |
| T_ATM_T3_03 | B72-A-19 | CopyEngine.cs | IsAtmBracketName | "Target1" -> true |
| T_ATM_T3_06 | B72-A-19 | CopyEngine.cs | IsAtmBracketName | "Target9" -> true |
| T_ATM_T3_07 | B72-A-19 | CopyEngine.cs | IsAtmBracketName | "PTT-BE-Stop" -> false (PTT prefix excluded) |
| T_ATM_T3_08 | B72-A-19 | CopyEngine.cs | IsAtmBracketName | "" (empty string) -> false |
| T_ATM_T3_09 | B72-A-20 | PttBreakEven.cs | CancelStaleBracketsLocal | "PTT-BE-Target-1" excluded (StartsWith "PTT-BE-" match) |
| T_ATM_T3_10 | B72-A-20 | PttBreakEven.cs | CancelStaleBracketsLocal | "Stop3" included in stale list (IsAtmBracketName true) |
| T_FOLLOWER_FLAT_01 | B72-A-21 | CopyEngine.cs | OnOrderUpdate | Follower Filled + Name "PTT-BE-Stop" + non-leader -> fires PositionStateChanged |
| T_FOLLOWER_FLAT_02 | B72-A-21 | CopyEngine.cs | OnOrderUpdate | Leader Filled + Name "PTT-BE-Stop" -> does NOT take narrow pre-Gate path |
| T_FOLLOWER_FLAT_03 | B72-A-21 | CopyEngine.cs | OnOrderUpdate | Follower Filled + Name NOT "PTT-BE-Stop" -> narrow path not triggered |
| T_FOLLOWER_FLAT_04 | B72-A-21 | CopyEngine.cs | OnOrderUpdate | Follower Cancelled + Name "PTT-BE-Stop" -> narrow path not triggered (Filled only) |
| T_DEDUP_MARKET_01 | B72-A-22 | CopyEngine.cs | IsDispatchTriggerState | Market + Submitted -> true |
| T_DEDUP_MARKET_02 | B72-A-22 | CopyEngine.cs | IsDispatchTriggerState | Market + Accepted -> false |
| T_DEDUP_LIMIT_01 | B72-A-22 | CopyEngine.cs | IsDispatchTriggerState | Limit + Accepted -> true |
| T_DEDUP_LIMIT_02 | B72-A-22 | CopyEngine.cs | IsDispatchTriggerState | Limit + Submitted -> false |
| T_QX_TARGETS_01 | B72-A-23 | CopyEngine.cs | MoveStopToBreakEven | Step A isAtmTarget includes "PTT-QX-T1" |
| T_QX_TARGETS_02 | B72-A-23 | CopyEngine.cs | MoveStopToBreakEven | Step A isAtmTarget includes "PTT-QX-T2" |
| T_QX_TARGETS_03 | B72-A-23 | CopyEngine.cs | MoveStopToBreakEven | Step A isAtmTarget includes "PTT-BE-Target-1" |
| T_QX_TARGETS_04 | B72-A-23 | CopyEngine.cs | MoveStopToBreakEven | Step A isAtmTarget includes "PTT-BE-Target-2" |
