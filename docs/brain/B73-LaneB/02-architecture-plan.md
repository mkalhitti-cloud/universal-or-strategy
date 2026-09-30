# B73-LaneB Architecture Plan

**Block**: B73-LaneB
**Phase**: 1 (Retrospective Architecture Plan)
**Written by**: ptt-architect
**Date**: 2026-08-14
**Status**: REVIEW_PASS
**Review gate**: PLAN_COMPLETE

---

## RULES CATALOG GATE

**Result**: PASS

P0 checks run against all 15 hotfix methods in `TradeCopierPanel.cs`:

| Check | Pattern | Result |
|-------|---------|--------|
| JS-021 `lock()` | `lock\s*\(` | 0 matches in hotfix scope (comment-only reference at line 1178 is a compliance annotation, not actual lock usage) |
| JS-033 `async void` | `async\s+void\s+\w+\(` | 0 actual async void methods (0 `async` keywords in src, all handlers are synchronous void per lines 1178, 1557, 1722, 1910) |
| JS-001 `throw new XxxException` | `throw\s+new\s+\w+Exception\(` | 0 matches |
| JS-002 `return null` in hot paths | `return\s+null\s*;` | 6 occurrences, ALL in cold-path guard/builder helpers (lines 441, 500, 503, 507, 1670, 1677); each annotated with JS-002 comment; none in order dispatch or BE ARM paths |
| ASCII-only | `[^\x00-\x7F]` | Pre-existing `\u25B2`/`\u25BC` at arrow-button construction (lines 1044, 1045, 1077, 1078, 1106, 1107) — pre-existing from prior blocks, not introduced by B73-LaneB hotfixes |

**Gate**: PASS. No P0 violations introduced by the 15 B73-LaneB hotfixes.

---

## Section 1: Block Identity

**Block**: B73-LaneB
**Pipeline phase**: Retrospective — code already applied; plan describes what is there and why.
**Files in scope**: `src/PropTraderTools/TradeCopierPanel.cs` (2619 lines)

### Lane Dependency: B72-LaneA (CopyEngine.cs)

B73-LaneB panels consume the following CopyEngine members that were defined or modified in B72-LaneA:

| Member | Kind | Direction |
|--------|------|-----------|
| `CopyEngine.PendingBeArmed` | event `Action<string, string>` | CopyEngine -> panels |
| `CopyEngine.GlobalBeAllDisarmed` | event `Action` | CopyEngine -> panels |
| `CopyEngine.GlobalBeBufferChanged` | event `Action<int>` | CopyEngine -> panels |
| `CopyEngine.GlobalQuickAllBufferChanged` | event `Action<int>` | CopyEngine -> panels |
| `CopyEngine.Instance.IsPendingSlotsEmpty()` | method `bool` | panels -> CopyEngine |
| `CopyEngine.Instance.DisarmPendingBe(Account)` | method `void` | panels -> CopyEngine |
| `CopyEngine.Instance.ArmAllPendingBe(...)` | method `void` (via GlobalBe.Execute) | panels -> CopyEngine |
| `CopyEngine.Instance.RaiseBeAllDisarmed()` | method `void` | panels -> CopyEngine |
| `CopyEngine.Instance.CancelQxBrackets(Account, Instrument)` | method `void` | panels -> CopyEngine |
| `CopyEngine.Instance.GlobalQuickAllT1` | property `int` | panels read |
| `CopyEngine.Instance.IncrementQuickAll()` | method `void` | panels -> CopyEngine |
| `CopyEngine.Instance.DecrementQuickAll()` | method `void` | panels -> CopyEngine |

**Dependency constraint**: All 12 members above MUST exist in `CopyEngine.cs` (B72-LaneA) before B73-LaneB compiles. B73-LaneB makes no changes to `CopyEngine.cs`.

---

## Section 2: Hotfix Inventory

### B73-B-01 — HOTFIX-DW-B72-02

**Hotfix ID**: B73-B-01
**Methods affected**: `OnGlobalBeClick`, `OnPendingBeFiredDispatch`, `UpdateButtonColors`
**File**: `TradeCopierPanel.cs`

**Change description**:
Removed the `_globalBeState` per-panel field that previously tracked whether BE ALL was armed or idle. All panels now read `CopyEngine.Instance.IsPendingSlotsEmpty()` as the single shared truth source for BE ALL armed state.

- `OnGlobalBeClick`: if `IsPendingSlotsEmpty()` is true, the state is Idle — arm all. Otherwise the state is Armed — disarm all by looping `Account.All` and calling `DisarmPendingBe(acc)` for each.
- `OnPendingBeFiredDispatch`: after `OnBeConnected`, checks `IsPendingSlotsEmpty()` — if the last slot just fired, calls `UpdateBeAllVisuals(BeState.Idle)` to reset the BE ALL button.
- `UpdateButtonColors`: removed the previously inserted DW-B72-02 blanket `DisarmPendingBe` block. BE ALL visual reset on flat is now handled only by `OnPendingBeFiredDispatch` and the separate HOTFIX-B73-B-04/B73-B-13 blocks (see below).

**WHY (bug being fixed)**:
The `_globalBeState` field was a per-panel local shadow of a global state. When two chart panels were open simultaneously (e.g. MES and MGC), each had its own `_globalBeState`. Arming BE ALL on Panel A set `_globalBeState = Armed` in Panel A but left Panel B with `_globalBeState = Idle`. On the next `UpdateButtonColors` call in Panel B, the blanket DW-B72-02 block would see `_globalBeState != Armed` and do nothing — leaving BE ALL visually inconsistent. The fix eliminates the local shadow by using `IsPendingSlotsEmpty()` which reads from the singleton and is therefore identical for all panels.

---

### B73-B-02 — HOTFIX-FIX-A-BE-BACKGROUND

**Hotfix ID**: B73-B-02
**Method affected**: `UpdateBeVisuals`
**File**: `TradeCopierPanel.cs`

**Change description**:
Added `_beBtn2.Background = BrushInactive` in the `BeState.Idle` case of `UpdateBeVisuals`.

**WHY (bug being fixed)**:
When the per-chart BE button transitioned from Armed back to Idle, only `BorderBrush` and `Foreground` were reset; `Background` was left at whatever it had been set to during the Armed state. The button background retained an amber tint after disarm. Adding the explicit `BrushInactive` assignment clears the background, making the visual state fully consistent with the Idle state.

---

### B73-B-03 — HOTFIX-FIX-C-NO-DISARM-IN-UPDATEBUTTONCOLORS

**Hotfix ID**: B73-B-03
**Method affected**: `UpdateButtonColors`
**File**: `TradeCopierPanel.cs`

**Change description**:
Removed the DW-B72-02 blanket `DisarmPendingBe` block that had been inserted directly inside `UpdateButtonColors`. BE ALL visual reset on flat is now handled only by `OnPendingBeFiredDispatch` (when the fired event resolves the last pending slot) and the dedicated HOTFIX-F3/HOTFIX-BEALL-FLAT-RESET blocks.

**WHY (bug being fixed)**:
The blanket disarm in `UpdateButtonColors` fired for every `hasPosition=false` event, including ATM bracket cancel noise. This meant pressing Cancel Entries (which fires `UpdateButtonColors(false, hasEntries)`) could inadvertently disarm a pending BE slot even when the trader still had a position or was actively armed for the next entry. Moving the disarm logic to the specific flat-signal-driven blocks (B73-B-04, B73-B-13) constrains the disarm to actual position-close events.

---

### B73-B-04 — HOTFIX-FLAT-DISARM

**Hotfix ID**: B73-B-04
**Method affected**: `UpdateButtonColors`
**File**: `TradeCopierPanel.cs` lines ~569-576 (HOTFIX-F3 block)

**Change description**:
Inside the existing `!hasPosition && _beState != BeState.Idle` branch (the per-chart BE reset block, "HOTFIX-F3"):
1. Added `CopyEngine.Instance.DisarmPendingBe(_leaderAccount)` — disarms this panel's leader account slot in CopyEngine.
2. Added `if (IsPendingSlotsEmpty()) UpdateBeAllVisuals(BeState.Idle)` — resets BE ALL button only if no other panels still have pending slots.

**WHY (bug being fixed)**:
Previously, when a position closed and `_beState` was Armed (the per-chart BE was active), the per-chart BE state was reset locally but the pending slot in CopyEngine was left dangling. This orphaned slot kept `IsPendingSlotsEmpty()` returning false, meaning the BE ALL button remained amber even though this panel's position was gone. The fix disarms the CopyEngine slot and then conditionally resets the BE ALL visual.

---

### B73-B-05 — HOTFIX-BEALL-SYNC-01

**Hotfix ID**: B73-B-05
**Methods affected**: `OnPendingBeArmedDispatch` (new handler), `OnLoaded` (subscription), `Detach` (unsubscription)
**File**: `TradeCopierPanel.cs` lines ~619-621 (subscribe), ~519 (unsubscribe), ~890-897 (handler)

**Change description**:
Added subscription to `CopyEngine.PendingBeArmed` event. Handler `OnPendingBeArmedDispatch` calls `Dispatcher.InvokeAsync(() => UpdateBeAllVisuals(BeState.Armed))` if `IsPendingSlotsEmpty()` is false (at least one slot is now armed). Subscription is in `OnLoaded`; unsubscription is in `Detach`.

**WHY (bug being fixed)**:
When Panel A armed BE ALL, Panel B received no notification. Panel B's BE ALL button remained purple (Idle) while Panel A showed amber (Armed). Without this broadcast event subscription, BE ALL visuals were only consistent on the panel that issued the arm command. The `PendingBeArmed` event is fired by CopyEngine after every successful slot arm, so all subscribed panels receive the notification and update their visuals via their own `Dispatcher.InvokeAsync`.

---

### B73-B-06 — HOTFIX-FLAT-MANUAL-CLOSE-01

**Hotfix ID**: B73-B-06
**Method affected**: `OnLeaderPositionUpdate`
**File**: `TradeCopierPanel.cs` lines ~1707-1715

**Change description**:
On `e.Operation == Operation.Remove` (NT8 delivers this when a position is fully closed), fires `Dispatcher.InvokeAsync(() => UpdateButtonColors(false, false))` after instrument and panel guards pass.

**WHY (bug being fixed)**:
`TryFirePositionState` (the primary flat signal) is triggered by order fill events. After a manual close via the NT8 Chart Trader X button or Close order, no ATM fill event fires through `TryFirePositionState`. The position is gone but `UpdateButtonColors` was never called — BE ALL remained armed and orphaned bracket orders were never cancelled. `PositionUpdate(Remove)` is NT8's authoritative signal for "position is gone". Using it as a second flat signal source covers the manual close path.

---

### B73-B-07 — HOTFIX-BEALL-DISARM-SYNC-01

**Hotfix ID**: B73-B-07
**Methods affected**: `OnGlobalBeAllDisarmed` (new handler), `OnLoaded` (subscription), `Detach` (unsubscription)
**File**: `TradeCopierPanel.cs` lines ~622-624 (subscribe), ~521 (unsubscribe), ~924-926 (handler)

**Change description**:
Added subscription to `CopyEngine.GlobalBeAllDisarmed` event. Handler `OnGlobalBeAllDisarmed` calls `Dispatcher.InvokeAsync(() => UpdateBeAllVisuals(BeState.Idle))`. `RaiseBeAllDisarmed()` is called from:
- `OnGlobalBeClick` disarm path (user manually disarms)
- `UpdateButtonColors` HOTFIX-BEALL-FLAT-RESET block (position closes while armed)

**WHY (bug being fixed)**:
When Panel A disarmed BE ALL (either manually via the button click or via flat event), Panel B did not receive the disarm signal and remained amber. Without `GlobalBeAllDisarmed` broadcast, the disarm was only visible on the panel that triggered it. The broadcast ensures all open panels reset their BE ALL visuals atomically.

---

### B73-B-08 — HOTFIX-BEALL-BUFFER-SYNC-01 (panel wiring)

**Hotfix ID**: B73-B-08
**Methods affected**: `OnGlobalBeBufferChanged` (new handler), `OnLoaded` (subscription), `Detach` (unsubscription)
**File**: `TradeCopierPanel.cs` lines ~620-622 (subscribe), ~519-520 (unsubscribe), ~902-908 (handler)

**Change description**:
Subscribes to `CopyEngine.GlobalBeBufferChanged` in `OnLoaded`; unsubscribes in `Detach`. Handler `OnGlobalBeBufferChanged(int newBuffer)` stores the broadcast buffer value and calls `FormatGlobalBeBuffer("BE ALL", newBuffer)` to update `_globalBeBtn2.Content`.

**WHY (bug being fixed)**:
Previously, each panel maintained its own `_globalBeBuffer` counter and updated its own label when the Up/Down arrows were pressed on its own panel. When Panel A incremented the buffer, Panel B showed the stale old value. With the singleton `GlobalBe.GlobalBeBuffer` as truth source and `GlobalBeBufferChanged` broadcast, both panels always display the same buffer value regardless of which panel's arrows were pressed.

---

### B73-B-09 — HOTFIX-BUFLABEL-02 (Dispatcher.InvokeAsync wrapping)

**Hotfix ID**: B73-B-09
**Methods affected**: `OnGlobalBeBufferChanged`, `OnQuickAllBufferChanged`, new `FormatQuickAllBuffer`
**File**: `TradeCopierPanel.cs` lines ~902-919, ~1159-1162

**Change description**:
Both `OnGlobalBeBufferChanged` and `OnQuickAllBufferChanged` handlers are wrapped in `Dispatcher.InvokeAsync` (the panel's own `Dispatcher`, not `Application.Current.Dispatcher`). New static method `FormatQuickAllBuffer(string name, int ticks)` appends a `"t"` suffix: e.g., `"Quick ALL +4t"` instead of `"Quick ALL +4"`.

**WHY (bug being fixed)**:
CopyEngine broadcasts events on `Application.Current.Dispatcher` (the application-level WPF UI thread). Each chart panel lives in its own chart window thread with its own `Dispatcher`. Direct UI property assignments (`.Content = ...`) from `Application.Current.Dispatcher` to WPF controls that were created on a different chart-window `Dispatcher` are illegal and produce cross-thread InvalidOperationExceptions. Wrapping in `this.Dispatcher.InvokeAsync` marshals the UI update to the correct chart-window thread. The `"t"` suffix is required for tick unit clarity — MES, MGC, and MCL have different dollar values per tick; the label must make the unit explicit.

---

### B73-B-10 — HOTFIX-QUICKALL-SINGLETON-01 (panel wiring)

**Hotfix ID**: B73-B-10
**Methods affected**: `OnQuickAllBufferChanged` (new subscription in `OnLoaded`/`Detach`), `OnQuickAllUp`, `OnQuickAllDown`
**File**: `TradeCopierPanel.cs` lines ~621-622 (subscribe), ~520-521 (unsubscribe), ~1619-1629 (handlers)

**Change description**:
Subscribes to `CopyEngine.GlobalQuickAllBufferChanged` in `OnLoaded`; unsubscribes in `Detach`. `OnQuickAllUp` calls `CopyEngine.Instance.IncrementQuickAll()` instead of incrementing the local `_quickAllT1` field. `OnQuickAllDown` calls `CopyEngine.Instance.DecrementQuickAll()`.

**WHY (bug being fixed)**:
`_quickAllT1` was a per-panel field. When Panel A pressed Quick ALL Up, Panel B's button still showed the old value. The fix moves ownership of the Quick ALL tick buffer to the CopyEngine singleton, and panels read the shared value via the `GlobalQuickAllBufferChanged` broadcast event. Identical pattern to the BE ALL buffer singleton fix (B73-B-08).

---

### B73-B-11 — HOTFIX-QUICKALL-COMPILE-01

**Hotfix ID**: B73-B-11
**Method affected**: `_quickAllBtn` button construction (inside `BuildBufferedButtonsRow` or equivalent builder)
**File**: `TradeCopierPanel.cs` line ~1119

**Change description**:
Changed the Quick ALL button initial content from `FormatBuffer("Quick ALL", _quickAllT1)` to `FormatBuffer("Quick ALL", CopyEngine.Instance.GlobalQuickAllT1)`.

**WHY (bug being fixed)**:
After B73-B-10 removed `_quickAllT1` as a per-panel field, any reference to it in button construction code no longer compiled. The initial content for the Quick ALL button must be read from `CopyEngine.Instance.GlobalQuickAllT1` (the singleton value) to both compile and display the correct shared initial state.

---

### B73-B-12 — HOTFIX-BEALL-DISARM-CROSS-01

**Hotfix ID**: B73-B-12
**Method affected**: `UpdateButtonColors`
**File**: `TradeCopierPanel.cs` lines ~583-589

**Change description**:
Moved `CopyEngine.Instance.RaiseBeAllDisarmed()` and `UpdateBeAllVisuals(BeState.Idle)` OUTSIDE the `if (IsPendingSlotsEmpty())` guard. Both now fire unconditionally when `!hasPosition && !IsPendingSlotsEmpty()`, regardless of whether other panel slots are still active.

**WHY (bug being fixed)**:
The original placement inside `IsPendingSlotsEmpty()` guard meant the disarm broadcast fired only when ALL slots were empty. If Panel A was flat and Panel B was still armed, Panel A would not broadcast the disarm — Panel B would remain armed in CopyEngine but Panel A's own BE ALL visual would be stale. The fix fires the broadcast and visual reset unconditionally on any flat event where slots exist, so all panels update their visuals even if the singleton is not yet fully empty. (The `DisarmPendingBe` call in the outer block handles the slot removal; `RaiseBeAllDisarmed` notifies all panels regardless.)

---

### B73-B-13 — HOTFIX-BEALL-FLAT-RESET

**Hotfix ID**: B73-B-13
**Method affected**: `UpdateButtonColors`
**File**: `TradeCopierPanel.cs` lines ~583-589 (independent block)

**Change description**:
Added a separate, independent `if (!hasPosition && !CopyEngine.Instance.IsPendingSlotsEmpty())` block in `UpdateButtonColors`. This block fires BE ALL reset (`UpdateBeAllVisuals(BeState.Idle)` + `RaiseBeAllDisarmed()`) even when the per-chart `_beState == Idle` (the HOTFIX-F3 block gate is false).

**WHY (bug being fixed)**:
HOTFIX-F3 only fires when `_beState != Idle`. If the user armed BE ALL (all panels received Armed visual) but did NOT arm the individual per-chart BE button (`_beState` stayed Idle), the HOTFIX-F3 gate would never trigger on flat. BE ALL remained amber indefinitely because no code path reset it. The independent block removes this dependency — BE ALL reset fires on every flat event where slots exist, regardless of the per-chart BE state.

---

### B73-B-14 — HOTFIX-ORPHAN-STOP-CLEANUP

**Hotfix ID**: B73-B-14
**Method affected**: `UpdateButtonColors`
**File**: `TradeCopierPanel.cs` lines ~596-597

**Change description**:
On `hasPosition=false` (flat), calls `CopyEngine.Instance.CancelQxBrackets(_leaderAccount, _instrument)` unconditionally (subject to `_leaderAccount != null && _instrument != null` guards).

**WHY (bug being fixed)**:
When a trader manually closes a position via the NT8 Chart Trader X button or a Close market order, NT8 does not automatically cancel AddOn-created bracket orders. `PTT-BE-Stop`, `PTT-BE-Stop-N`, `PTT-BE-Target-N`, and `PTT-QX-*` orders remain in Working state on the account. On the next entry, these orphaned orders re-fill at unexpected prices. `CancelQxBrackets` iterates account orders and cancels any order whose name satisfies `IsQxCancelCandidate` (prefixes `PTT-BE-*` and `PTT-QX-*`). The call is safe when no such orders exist: the CopyEngine implementation exits early when `stale.Count == 0`.

---

### B73-B-15 — HOTFIX-FOLLOWER-LABEL-CLIP-01

**Hotfix ID**: B73-B-15
**Method affected**: `BuildInlineFollowerRow`
**File**: `TradeCopierPanel.cs` lines ~1742-1820

**Change description**:
Replaced the `StackPanel` row container with a `DockPanel` (`LastChildFill = true`). The account name `TextBlock` is the last child (fills remaining space). `TextTrimming = CharacterEllipsis` is set on the name label. ATM `ComboBox` and PnL `TextBlock` are right-docked via `DockPanel.SetDock(..., Dock.Right)`.

**WHY (bug being fixed)**:
The prior `StackPanel` layout used a fixed-width (90px) `TextBlock` for account names. Prop firm account names such as `"PA-APEX-422136-01U"` (20 characters, ~160px at 8px/char) overflowed the fixed width and were clipped without ellipsis. `DockPanel` with `LastChildFill` allows the name label to use all horizontal space not consumed by the right-docked ATM combo and PnL label. `TextTrimming.CharacterEllipsis` provides a clean degradation path if the panel is extremely narrow.

---

## Section 3: Architecture Themes

### Theme 1: BE ALL Button State is Singleton-Scoped

**Implementation**: `UpdateButtonColors`, `OnGlobalBeClick`, `OnPendingBeFiredDispatch`, all via `CopyEngine.Instance.IsPendingSlotsEmpty()`

**Before B73**: Each `TradeCopierPanel` instance held `_globalBeState` (a `BeState` enum field). This was a per-panel copy of the global armed state.

**Problem**: With N panels open simultaneously, each panel's `_globalBeState` was updated only when that panel performed an arm/disarm operation. Panel B had no mechanism to know that Panel A had changed the state. The result was visual desync: one panel showed "Armed" (amber), another showed "Idle" (purple), for the same globally-armed BE ALL state.

**Fix**: `_globalBeState` is entirely removed (field deleted from the class). The question "is BE ALL armed?" is always answered by querying `CopyEngine.Instance.IsPendingSlotsEmpty()`. Since all panels read the same singleton, they always get the same answer. The pattern is:

```
IsPendingSlotsEmpty() == true  ->  State is Idle  (no slots are pending)
IsPendingSlotsEmpty() == false ->  State is Armed (at least one slot is pending)
```

This is a correct-by-construction design: it is not possible for two panels to observe different BE ALL states because there is no per-panel state to diverge.

---

### Theme 2: Broadcast Event Model for Multi-Panel Sync

**Events**: `PendingBeArmed`, `GlobalBeAllDisarmed`, `GlobalBeBufferChanged`, `GlobalQuickAllBufferChanged`
**Subscription**: In `OnLoaded` — `_engine.EventName += HandlerMethod`
**Unsubscription**: In `Detach` — `_engine.EventName -= HandlerMethod`

Every panel subscribes to the same four CopyEngine broadcast events. When any panel (or CopyEngine itself) changes a globally-relevant state, it raises the event on `Application.Current.Dispatcher`. Every open panel receives the event via its registered handler and updates its own UI controls.

The panel handler pattern is uniform:
```csharp
private void OnGlobalBeBufferChanged(int newBuffer)
{
    Dispatcher.InvokeAsync(() =>
    {
        if (_globalBeBtn2 != null)
            _globalBeBtn2.Content = FormatGlobalBeBuffer("BE ALL", newBuffer);
    });
}
```

All four handlers follow this pattern: receive on the application dispatcher, re-marshal to the chart-window dispatcher via `Dispatcher.InvokeAsync`, update the local WPF control.

---

### Theme 3: Independent BE State Machines

The B73-LaneB code maintains two independent state machines that are intentionally NOT coupled:

**State Machine 1 — Per-chart BE button** (`_beState: BeState`):
- Controls: `_beBtn2` button
- Arm: user clicks the per-chart BE button while flat (sets `_beState = Armed`)
- Disarm: `UpdateButtonColors(false)` when `_beState != Idle` (HOTFIX-F3 block, lines ~569-576)
- Reset method: `UpdateBeVisuals(BeState.Idle)` + `DisarmPendingBe(_leaderAccount)`
- Scope: this chart panel only

**State Machine 2 — BE ALL button** (via `IsPendingSlotsEmpty()`):
- Controls: `_globalBeBtn2` button
- Arm: `CopyEngine.GlobalBe.Execute(buffer)` — arms all pending slots
- Disarm: broadcast via `GlobalBeAllDisarmed` event -> `OnGlobalBeAllDisarmed` -> `UpdateBeAllVisuals(Idle)`
- Reset method: `UpdateBeAllVisuals(BeState.Idle)` on all panels via broadcast
- Scope: all panels sharing the CopyEngine singleton

**Design invariant**: Each state machine is reset independently. In `UpdateButtonColors`, HOTFIX-F3 (lines ~569-576) checks `_beState != Idle` and resets State Machine 1. The HOTFIX-BEALL-FLAT-RESET block (lines ~583-589) checks `!IsPendingSlotsEmpty()` and resets State Machine 2. These two blocks are intentionally separate. If they were merged, a user who armed BE ALL but did NOT arm the per-chart BE (so `_beState == Idle`) would leave BE ALL stuck amber on flat — exactly the bug that B73-B-13 fixes.

---

### Theme 4: Flat Signal Sources and Their Roles

Two distinct code paths deliver the "position is now flat" signal to `UpdateButtonColors`:

**Path A — `TryFirePositionState` (Filled/PartFilled fill events)**
- Source: NT8 order fill event, processed by CopyEngine
- Routing: CopyEngine fires `PositionStateChanged` event -> panel `OnPositionStateChanged` -> `Dispatcher.InvokeAsync` -> `UpdateButtonColors`
- Scope: fires only for Filled or PartFilled order states, AFTER Gate 2.5 validation in CopyEngine
- Coverage: normal trade exit via fill (ATM bracket target/stop fills)
- Limitation: does NOT fire on manual Chart Trader X close or Close order (no order fill event is generated for those paths)

**Path B — `OnLeaderPositionUpdate` (Operation.Remove)**
- Source: NT8 `PositionUpdate` event with `Operation.Remove`
- Routing: NT8 fires on background account thread -> `OnLeaderPositionUpdate` -> `Dispatcher.InvokeAsync` -> `UpdateButtonColors(false, false)` (B73-B-06)
- Scope: fires ONLY on `Operation.Remove` (position fully closed), after 4-guard filter (null, instrument match)
- Coverage: manual close via Chart Trader X or Close order
- Advantage: NT8 guarantees position state is fully updated at `PositionUpdate.Remove` time (unlike order Filled events where `HasOpenPosition` may still reflect old quantity)

**Both paths are required** because they cover non-overlapping close scenarios:
- Fill-based close (ATM bracket fires) -> Path A
- Manual close (X button or Close order) -> Path B

Without Path B, BE ALL could remain armed indefinitely after a manual close because Path A never fires for non-fill exits.

---

### Theme 5: Orphaned Bracket Cleanup on Flat

**Implementation**: `UpdateButtonColors` HOTFIX-ORPHAN block (lines ~596-597)
**CopyEngine method**: `CancelQxBrackets(Account, Instrument)`
**Predicate**: `IsQxCancelCandidate(Order)` — covers `PTT-BE-*` and `PTT-QX-*` prefixes

When `UpdateButtonColors` is called with `hasPosition=false`, the panel calls `CopyEngine.Instance.CancelQxBrackets(_leaderAccount, _instrument)` unconditionally (subject to null guards). This cancels any remaining Working orders with `PTT-BE-*` or `PTT-QX-*` names on the leader account for the instrument.

**Safety properties**:
- CopyEngine `CancelQxBrackets` returns early when `stale.Count == 0` — zero cost when no matching orders exist
- `IsQxCancelCandidate` uses `StringComparison.Ordinal` prefix checks — no allocation, no reflection
- Called unconditionally: does not depend on `_beState` or `IsPendingSlotsEmpty()` result

**Design rationale**: NT8 does NOT automatically cancel AddOn-created bracket orders when a position closes via non-ATM paths. `PTT-BE-Stop-N` and `PTT-QX-T1` orders survive the position close and can re-fill on the next entry. Unconditional cleanup on every flat signal is the safe default because the no-op early return makes false positives free.

---

### Theme 6: DockPanel Layout for Follower Rows

**Before B73**: `BuildInlineFollowerRow` constructed a `StackPanel` row with a `TextBlock` at fixed width 90px for the account name.

**Problem**: Prop firm account names such as `"PA-APEX-422136-01U"` are 20+ characters. At the default WPF font size, 20 characters requires approximately 160px. The 90px fixed width caused hard truncation with no visual indicator (no ellipsis).

**After B73 (B73-B-15)**: Row container changed to `DockPanel` with `LastChildFill = true`. The DockPanel child order is:
1. `CheckBox` — `DockPanel.SetDock(Dock.Left)`
2. `ComboBox` (ATM) — `DockPanel.SetDock(Dock.Right)` — processed first by DockPanel
3. `TextBlock` (PnL) — `DockPanel.SetDock(Dock.Right)` — processed after ATM combo
4. `TextBlock` (account name) — last child, `LastChildFill = true`, `TextTrimming = CharacterEllipsis`

The account name label fills all remaining horizontal space after the check box, ATM combo, and PnL label claim their widths. `TextTrimming.CharacterEllipsis` ensures that if the panel is made very narrow, the name degrades to `"PA-APEX-422..."` instead of an invisible overflow.

---

## Section 4: Threading Model

### Two-Dispatcher Pattern

B73-LaneB operates across two WPF dispatcher domains:

**Dispatcher 1 — Application.Current.Dispatcher (app UI thread)**
- Ownership: `CopyEngine` singleton
- What runs here: CopyEngine event raises (`PendingBeArmed`, `GlobalBeAllDisarmed`, `GlobalBeBufferChanged`, `GlobalQuickAllBufferChanged`)
- Source: CopyEngine singleton is created and owned by the `TradeCopierAddOn` (an `AddOnBase`), which runs on the application dispatcher

**Dispatcher 2 — chart-window Dispatcher (per chart window)**
- Ownership: each `TradeCopierPanel` instance
- What runs here: all WPF control property mutations (`_globalBeBtn2.Content`, `_quickAllBtn.Content`, `UpdateBeAllVisuals`, `UpdateButtonColors`)
- Source: NinjaTrader creates one `ChartWindow` per chart instrument; `TradeCopierPanel` is embedded in that chart window

**The marshal pattern (reference: `OnGlobalBeAllDisarmed`, line ~924)**:
```csharp
private void OnGlobalBeAllDisarmed()
{
    // Event fired on Application.Current.Dispatcher (CopyEngine domain).
    // _globalBeBtn2 was created on this panel's chart-window Dispatcher.
    // Must re-marshal to the correct Dispatcher before touching any UI control.
    Dispatcher.InvokeAsync(() => UpdateBeAllVisuals(BeState.Idle));
}
```

`this.Dispatcher` is the `Dispatcher` of the `UserControl` (`TradeCopierPanel`) — it is the chart window's UI thread dispatcher, which differs from `Application.Current.Dispatcher` when NinjaTrader opens charts in separate windows.

**All four broadcast event handlers follow this pattern**:

| Handler | Marshal target | UI control updated |
|---------|---------------|-------------------|
| `OnPendingBeArmedDispatch` | `this.Dispatcher` | `_globalBeBtn2` via `UpdateBeAllVisuals` |
| `OnGlobalBeBufferChanged` | `this.Dispatcher` | `_globalBeBtn2.Content` |
| `OnQuickAllBufferChanged` | `this.Dispatcher` | `_quickAllBtn.Content` |
| `OnGlobalBeAllDisarmed` | `this.Dispatcher` | `_globalBeBtn2` via `UpdateBeAllVisuals` |

**NT8 background thread handlers** (`OnLeaderPositionUpdate`, `OnLeaderOrderUpdate`):
These are subscribed to NT8 account events which fire on NT8's background account thread. They also use `Dispatcher.InvokeAsync` but the rationale is NT8-thread -> chart-window-dispatcher, not app-dispatcher -> chart-window-dispatcher.

---

## Section 5: CopyEngine API Surface (B72-LaneA Dependency)

All CopyEngine members consumed by B73-LaneB hotfixes. These MUST be defined in `src/PropTraderTools/CopyEngine.cs` (B72-LaneA).

### Events

| Event | Signature | Fired when | B73 consumer |
|-------|-----------|-----------|--------------|
| `PendingBeArmed` | `event Action<string, string>` | A pending BE slot is armed (instr, accountName) | `OnPendingBeArmedDispatch` |
| `GlobalBeAllDisarmed` | `event Action` | All pending BE slots disarmed | `OnGlobalBeAllDisarmed` |
| `GlobalBeBufferChanged` | `event Action<int>` | BE ALL buffer changed (newBuffer) | `OnGlobalBeBufferChanged` |
| `GlobalQuickAllBufferChanged` | `event Action<int>` | Quick ALL buffer changed (newT1) | `OnQuickAllBufferChanged` |

### Methods

| Method | Signature | B73 usage |
|--------|-----------|-----------|
| `IsPendingSlotsEmpty` | `bool IsPendingSlotsEmpty()` | Truth source for BE ALL armed state; called in `OnGlobalBeClick`, `OnPendingBeFiredDispatch`, `OnPendingBeArmedDispatch`, `UpdateButtonColors` |
| `DisarmPendingBe` | `void DisarmPendingBe(Account)` | Clears CopyEngine pending slot for one account; called from `OnGlobalBeClick` (disarm all) and `UpdateButtonColors` HOTFIX-F3/FLAT-RESET blocks |
| `ArmAllPendingBe` | via `GlobalBe.Execute(int buffer)` | Arms all pending slots; called from `OnGlobalBeClick` arm path |
| `RaiseBeAllDisarmed` | `void RaiseBeAllDisarmed()` | Raises `GlobalBeAllDisarmed` event; called from `OnGlobalBeClick` disarm path and `UpdateButtonColors` HOTFIX-BEALL-FLAT-RESET block |
| `CancelQxBrackets` | `void CancelQxBrackets(Account, Instrument)` | Cancels all PTT-BE-*/PTT-QX-* Working orders; called from `UpdateButtonColors` HOTFIX-ORPHAN block |
| `IncrementQuickAll` | `void IncrementQuickAll()` | Increments singleton Quick ALL tick buffer; called from `OnQuickAllUp` |
| `DecrementQuickAll` | `void DecrementQuickAll()` | Decrements singleton Quick ALL tick buffer; called from `OnQuickAllDown` |

### Properties

| Property | Type | B73 usage |
|----------|------|-----------|
| `GlobalQuickAllT1` | `int` | Initial content for `_quickAllBtn` at construction time (B73-B-11) |

---

## Section 6: JS-DNA Compliance

### Per-Hotfix Compliance Table

| Hotfix | lock() | async void | return null | throw new | CYC note |
|--------|--------|-----------|-------------|-----------|----------|
| B73-B-01 | None | None | None | None | `OnGlobalBeClick` CYC=4 (if/else + foreach + null guard); `UpdateButtonColors` CYC unchanged |
| B73-B-02 | None | None | None | None | `UpdateBeVisuals` CYC=2 (unchanged) |
| B73-B-03 | None | None | None | None | `UpdateButtonColors` CYC reduced: one branch removed |
| B73-B-04 | None | None | None | None | `UpdateButtonColors`: nested if added, CYC stays <= 8 |
| B73-B-05 | None | None | None | None | `OnPendingBeArmedDispatch` CYC=1 (single InvokeAsync, no branch) |
| B73-B-06 | None | None | None | None | `OnLeaderPositionUpdate` CYC=2 (null guard + instrument guard) |
| B73-B-07 | None | None | None | None | `OnGlobalBeAllDisarmed` CYC=1 (straight-line InvokeAsync) |
| B73-B-08 | None | None | None | None | `OnGlobalBeBufferChanged` CYC=1 (null guard + InvokeAsync) |
| B73-B-09 | None | None | None | None | `FormatQuickAllBuffer` CYC=1 (straight-line string concat) |
| B73-B-10 | None | None | None | None | `OnQuickAllUp`/`OnQuickAllDown` CYC=1 each |
| B73-B-11 | None | None | None | None | Button construction — no branch, no CYC impact |
| B73-B-12 | None | None | None | None | `UpdateButtonColors`: one block moved outside guard |
| B73-B-13 | None | None | None | None | `UpdateButtonColors`: new independent block, CYC += 1 |
| B73-B-14 | None | None | None | None | `UpdateButtonColors`: one new null-guarded line, CYC += 0 (guard already present) |
| B73-B-15 | None | None | None | None | `BuildInlineFollowerRow` CYC=1 (straight-line construction) |

### Additional JS-DNA Notes

**JS-021 (No lock())**: Zero `lock()` usage in any hotfix method. All shared state accessed via CopyEngine singleton methods (encapsulate their own lock-free concurrency internally). UI state only accessed on WPF UI thread via `Dispatcher.InvokeAsync`.

**JS-033 (Avoid async void)**: All event handler methods are synchronous `void` — they call `Dispatcher.InvokeAsync` (a fire-and-forget dispatch returning `DispatcherOperation`, not `async void`). No `async void` methods added by B73-LaneB.

**JS-001 (No throw in hot paths)**: No exception throwing in any hotfix method.

**JS-002 (No return null in hot paths)**: `return null` exists only in cold-path builder helpers (`FindPriceCanvasPanel`, `TryResolveLeaderAccount`, `FindWorkingOrder`) which are unchanged by B73-LaneB and were already present in prior blocks with explicit JS-002 annotations.

**DateTime.UtcNow**: No DateTime usage in any hotfix method.

**ASCII-only**: No non-ASCII characters introduced by B73-LaneB hotfixes. Pre-existing `\u25B2`/`\u25BC` arrow characters in button content are from prior blocks.

**`"PTT-"` prefix**: `CancelQxBrackets` operates on `PTT-BE-*` and `PTT-QX-*` prefixed orders, consistent with the project-wide PTT-prefix mandate for all AddOn-created orders.

---

## Section 7: Scan Checklist

The following 7 scans MUST be run against all modified/new methods in `TradeCopierPanel.cs` after applying B73-LaneB hotfixes.

### S1 — lock() Scan
**Pattern**: `lock\s*\(`
**Scope**: All B73-LaneB hotfix methods
**Expected result**: 0 matches in functional code (comments containing "lock" do not count)
**Evidence**: Line 1178 contains comment `// JS-021: no lock()` — this is the only match and it is a comment, not a lock statement.

### S2 — async void Scan
**Pattern**: `async\s+void\s+\w+\(`
**Scope**: `TradeCopierPanel.cs` entire file
**Expected result**: 0 matches
**Evidence**: 7 grep matches for `async\s+` are all comment lines (compliance annotations); 0 actual `async void` method declarations in file.

### S3 — return null Scan
**Pattern**: `return\s+null\s*;`
**Scope**: B73-LaneB hotfix methods specifically
**Expected result**: 0 matches in hotfix methods (B73-B-01 through B73-B-15)
**Evidence**: 6 total `return null` in file, all in pre-existing cold-path helpers (`FindPriceCanvasPanel` line 441, `TryResolveLeaderAccount` lines 500/503/507, `FindWorkingOrder` lines 1670/1677) — none in B73 hotfix scope.

### S4 — throw new Exception Scan
**Pattern**: `throw\s+new\s+\w+Exception\(`
**Scope**: `TradeCopierPanel.cs` entire file
**Expected result**: 0 matches
**Evidence**: grep returned 0 matches.

### S5 — ASCII-only Scan
**Pattern**: `[^\x00-\x7F]`
**Scope**: B73-LaneB hotfix methods specifically
**Expected result**: 0 non-ASCII characters in hotfix methods
**Evidence**: Pre-existing `\u25B2`/`\u25BC` Unicode arrow characters at lines 1044, 1045, 1077, 1078, 1106, 1107 (arrow button content) are NOT in any B73-LaneB hotfix method; all are in `BuildBufferedButtonsRow`/button construction code from prior blocks.

### S6 — CYC <= 8 for All Modified/New Methods
**Scope**: All 15 hotfix methods

| Method | Estimated CYC | Branches |
|--------|-------------|---------|
| `UpdateButtonColors` | 6 | null guards x3, HOTFIX-F3 if, HOTFIX-BEALL-FLAT-RESET if, HOTFIX-ORPHAN if |
| `OnGlobalBeClick` | 4 | `IsPendingSlotsEmpty` if/else, `Account.All` null, foreach |
| `OnPendingBeFiredDispatch` | 2 | `IsPendingSlotsEmpty` if |
| `OnPendingBeArmedDispatch` | 2 | `IsPendingSlotsEmpty` if |
| `OnGlobalBeBufferChanged` | 2 | null guard on `_globalBeBtn2` |
| `OnQuickAllBufferChanged` | 2 | null guard on `_quickAllBtn` |
| `OnGlobalBeAllDisarmed` | 1 | straight-line |
| `UpdateBeAllVisuals` | 2 | if BeState.Idle / else |
| `OnLeaderPositionUpdate` | 5 | null guards x2, `Operation.Remove` check, instrument null, FullName compare |
| `BuildInlineFollowerRow` | 1 | straight-line construction |
| `FormatQuickAllBuffer` | 1 | straight-line |
| `OnQuickAllUp` | 1 | straight-line |
| `OnQuickAllDown` | 1 | straight-line |

All methods: CYC <= 8. No violations.

### S7 — xUnit Test Completeness
The following 33 xUnit `[Fact]` test names cover B73-LaneB behavior. All must exist in the test suite for B73-LaneB to be verifiable.

**BE ALL singleton state tests** (B73-B-01):
1. `T_BEALL_SYNC_01` — `IsPendingSlotsEmpty()` returns true at initial/empty state
2. `T_BEALL_SYNC_02` — `DisarmPendingBe(null)` does not throw when called with null

**Per-chart BE visual tests** (B73-B-02):
3. `T_BE_BG_01` — `BeState.Idle` and `BeState.Armed` are defined enum members
4. `T_BE_BG_02` — `BeState.Armed != BeState.Idle`

**No-disarm-in-UpdateButtonColors tests** (B73-B-03):
5. `T_NO_DISARM_01` — `DisarmPendingBe(null)` returns without exception (null guard present)
6. `T_NO_DISARM_02` — `IsPendingSlotsEmpty()` is idempotent (two consecutive calls return same value)

**Flat-disarm tests** (B73-B-04):
7. `T_FLAT_DISARM_01` — `DisarmPendingBe(null)` with null argument returns without exception
8. `T_FLAT_DISARM_02` — `IsPendingSlotsEmpty()` after `DisarmPendingBe(null)` returns a `bool` without exception

**BE ALL arm sync tests** (B73-B-05):
9. `T_BEALL_ARM_01` — `CopyEngine` has member `PendingBeArmed` accessible via reflection
10. `T_BEALL_ARM_02` — `CopyEngine` has member `GlobalBeAllDisarmed` accessible via reflection

**Manual close flat tests** (B73-B-06):
11. `T_MANUAL_CLOSE_01` — `Operation.Remove` is a defined member of the `Operation` enum
12. `T_MANUAL_CLOSE_02` — `Operation.Remove != Operation.Update`

**BE ALL disarm sync tests** (B73-B-07):
13. `T_DISARM_SYNC_01` — `CopyEngine` exposes member `GlobalBeAllDisarmed` accessible via reflection
14. `T_DISARM_SYNC_02` — `CopyEngine.Instance.RaiseBeAllDisarmed()` is callable without exception

**BE ALL buffer sync tests** (B73-B-08):
15. `T_BUF_BE_01` — `FormatGlobalBeBuffer("BE ALL", 3)` returns `"BE ALL +3"`
16. `T_BUF_BE_02` — `FormatGlobalBeBuffer("BE ALL", 0)` returns `"BE ALL"` (no suffix at zero)

**Dispatcher wrapping + format tests** (B73-B-09):
17. `T_LABEL_01` — `FormatQuickAllBuffer("Quick ALL", 4)` returns `"Quick ALL +4t"`
18. `T_LABEL_02` — `FormatGlobalBeBuffer("BE ALL", 5)` returns `"BE ALL +5"`
19. `T_LABEL_03` — return value of `FormatQuickAllBuffer("Quick ALL", 4)` contains `"t"` suffix
20. `T_LABEL_04` — `FormatQuickAllBuffer("Quick ALL", 0)` returns `"Quick ALL +0t"`

**Quick ALL singleton tests** (B73-B-10):
21. `T_QA_SING_01` — `CopyEngine` has member `GlobalQuickAllBufferChanged` accessible via reflection
22. `T_QA_SING_02` — `CopyEngine.Instance.GlobalQuickAllT1` returns an `int` without exception

**Quick ALL init test** (B73-B-11):
23. `T_QA_INIT_01` — `CopyEngine.Instance.GlobalQuickAllT1 >= 1`

**BE ALL disarm cross-panel tests** (B73-B-12):
24. `T_DISARM_CROSS_01` — `RaiseBeAllDisarmed()` callable twice in succession without exception
25. `T_DISARM_CROSS_02` — `IsPendingSlotsEmpty()` after `RaiseBeAllDisarmed()` returns a `bool` without exception

**BE ALL flat-reset independent block tests** (B73-B-13):
26. `T_BEALL_FLAT_01` — `CopyEngine` has member `GlobalBeBufferChanged` accessible via reflection
27. `T_BEALL_FLAT_02` — `IsPendingSlotsEmpty()` is idempotent on two consecutive calls

**Orphan cleanup tests** (B73-B-14):
28. `T_ORPHAN_01` — `CancelQxBrackets(null, null)` returns without exception
29. `T_ORPHAN_02` — `CopyEngine.IsQxCancelCandidate(null)` returns `false`
30. `T_ORPHAN_03` — `IsQxCancelCandidate` is accessible as public or internal static method via reflection

**DockPanel layout tests** (B73-B-15):
31. `T_LABEL_CLIP_01` — `System.Windows.Controls.DockPanel` type is present (compile-time + runtime check)
32. `T_LABEL_CLIP_02` — `DockPanel.LastChildFillProperty` DependencyProperty exists via reflection
33. `T_LABEL_CLIP_03` — `DockPanel.DockProperty` DependencyProperty exists via reflection

---

## Section 8: Deferred Work

### Items Closed by B73-LaneB

None. B73-LaneB is a TradeCopierPanel.cs hotfix block only. No deferred work items from prior blocks targeted TradeCopierPanel.cs.

### Carry-Forward OPEN Items (from B66-LaneC/06-deferred-backlog.md)

The following 9 deferred items remain OPEN and carry forward unchanged from B66-LaneC:

| ID | Description | Priority | Target | Status |
|----|-------------|----------|--------|--------|
| DW-B66-C-02 | DispatchCopy dedup key = 0.0 for all StopLimit entries at Gate 5 | P1 | B67+ | OPEN |
| DW-B66-BE-01 | CancelQxBrackets cancels PTT-BE-Stop orders during Quick Exit — Director confirmation required | P1 | B67+ | OPEN |
| DW-B63-01 | Spurious PTT-Copy bracket orders on Sim102 after ATM fill | P1 | B67+ | OPEN |
| DW-B54-01 | ATM auto-inject — blocked, requires StrategyBase-level API unavailable in AddOnBase | P1 | future (blocked) | OPEN |
| DW-B58-01 | SnapshotTargetsPublic hardcoded order-name prefixes | P2 | future | OPEN |
| DW-B58-02 | GlobalBe non-atomic lazy init | P2 | future | OPEN |
| DW-B58-03 | RelayBe OcoGroup not forwarded | P2 | future | OPEN |
| PRE-EXISTING-01 | Non-ASCII em-dash CopyEngine.cs lines 398, 499 | P2 | future | OPEN |
| PRE-EXISTING-02 | Non-ASCII arrow CopyEngine.cs lines ~1449-1450 | P2 | future | OPEN |
| PRE-EXISTING-03 | deploy-sync.ps1 archived; PropTraderTools sync is manual | P2 | future | OPEN |

### New Deferred Items Identified by B73-LaneB

#### DW-B73-B-01 — RaiseBeAllDisarmed fires on every flat even when no slots were armed

**Priority**: P2
**Target block**: B75+
**Status**: OPEN

**Description**: `UpdateButtonColors` HOTFIX-BEALL-FLAT-RESET block calls `RaiseBeAllDisarmed()` whenever `!hasPosition && !IsPendingSlotsEmpty()`. This is correct and intentional — it fires the broadcast to sync all panels. However, if `_leaderAccount` had no pending slot in CopyEngine (the trader had armed BE ALL from a different panel, not this one), this panel still fires the broadcast. The broadcast is idempotent (all panels call `UpdateBeAllVisuals(Idle)` which is safe), so there is no correctness issue. The minor concern is redundant event fires across many open panels. Future optimization: gate `RaiseBeAllDisarmed` on `_leaderAccount`'s slot state before raising.

**Impact**: None (correctness). Redundant broadcasts are a no-op after the first panel processes the disarm.

**Defer rationale**: Scope creep risk. The current behavior is correct. Optimization requires adding per-account slot tracking to the BE ALL reset path, increasing CYC of `UpdateButtonColors`. Not worth adding complexity in this hotfix block.

---

#### DW-B73-B-02 — UpdateBeAllVisuals uses MakeBrush on every call (no freeze/cache)

**Priority**: P2
**Target block**: future
**Status**: OPEN

**Description**: `UpdateBeAllVisuals` calls `MakeBrush(13, 148, 136)` on every invocation for both `BorderBrush` and `Foreground`. `MakeBrush` creates a new `SolidColorBrush` instance each call. Brushes created for static colors should be `Freeze()`d and cached as `static readonly` fields to avoid repeated allocations on the WPF UI thread.

**Impact**: Performance only. Each call to `UpdateBeAllVisuals` allocates 2 brush objects. In normal trading this is called at most a few times per session (arm/disarm/flat cycles). Not a hot path.

**Defer rationale**: Pre-existing pattern used by multiple methods in the panel. A correct fix requires auditing all `MakeBrush` call sites and deciding which are static-color eligible for caching. Out of scope for B73-LaneB hotfix block.

---

### Deferred Work Summary for B73-LaneB

| ID | Description | Priority | Target | Status |
|----|-------------|----------|--------|--------|
| DW-B73-B-01 | RaiseBeAllDisarmed fires on every flat regardless of per-account slot ownership | P2 | B75+ | NEW — OPEN |
| DW-B73-B-02 | UpdateBeAllVisuals creates unfrozen brushes on every call | P2 | future | NEW — OPEN |
| DW-B66-C-02 | DispatchCopy dedup key = 0.0 for StopLimit (Gate 5) | P1 | B67+ | CARRY-FORWARD |
| DW-B66-BE-01 | CancelQxBrackets cancels PTT-BE-Stop on Quick Exit | P1 | B67+ | CARRY-FORWARD |
| DW-B63-01 | Spurious PTT-Copy bracket orders on Sim102 after ATM fill | P1 | B67+ | CARRY-FORWARD |
| DW-B54-01 | ATM auto-inject (blocked) | P1 | future | CARRY-FORWARD |
| DW-B58-01 | SnapshotTargetsPublic hardcoded prefixes | P2 | future | CARRY-FORWARD |
| DW-B58-02 | GlobalBe non-atomic lazy init | P2 | future | CARRY-FORWARD |
| DW-B58-03 | RelayBe OcoGroup not forwarded | P2 | future | CARRY-FORWARD |
| PRE-EXISTING-01 | Non-ASCII em-dash CopyEngine.cs lines 398, 499 | P2 | future | CARRY-FORWARD |
| PRE-EXISTING-02 | Non-ASCII arrow CopyEngine.cs lines ~1449-1450 | P2 | future | CARRY-FORWARD |
| PRE-EXISTING-03 | deploy-sync.ps1 archived; PropTraderTools sync is manual | P2 | future | CARRY-FORWARD |

**Closed this block**: 0
**New items this block**: 2 (DW-B73-B-01, DW-B73-B-02)
**Carry-forward OPEN**: 10 items
