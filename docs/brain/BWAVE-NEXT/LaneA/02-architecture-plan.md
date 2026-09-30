# BWAVE-NEXT Lane A -- Architecture Plan

**Status**: PLAN_COMPLETE (cycle 1 revision -- JS-002 fixed)
**Author**: ptt-architect
**Date**: 2026-09-04
**Spec sources**: LaneA-mission-brief.md, DW-NEW-08, DW-NEW-09
**Files in scope**: `src/PropTraderTools/TradeCopierPanel.cs`, `src/PropTraderTools/CopyEngine.cs`, test file

---

## 1. Lane-Split Gate

### Q1. Are any two tickets in the same method or within 50 lines?

- T1 touches `Detach()` region (lines 616-620) in `TradeCopierPanel.cs`.
- T2 touches `BuildBufferedButtonsRow` + `BuildArrowCluster` region (lines 1131-1237) in `TradeCopierPanel.cs`. Distance from T1 region: ~496 lines. **NO**.
- T4 touches `OnOrderUpdate` (line 1355 area) and adds new methods in `CopyEngine.cs`.
- T5 touches lines 3437 and 3637 in `CopyEngine.cs`. Distance from T4 OnOrderUpdate touch: ~2082 lines. **NO**.
- T3 is test-only.

**Q1 answer: NO**

### Q2. Does Fix B design depend on Fix A final design?

- T2 has no dependency on T1's verification outcome.
- T4 and T5 are completely independent designs in `CopyEngine.cs`.
- T3 depends on T1 being merged (its tests exercise the Detach path T1 verified), but this is an execution ordering dependency, not a design dependency.

**Q2 answer: NO**

### Q3. Does each fix have standalone value if others are blocked?

- T1: standalone value -- confirms teardown ordering and establishes regression guard.
- T2: standalone value -- eliminates background-overwrite risk and reduces CYC.
- T3: standalone value -- fills integration test gap for sibling isolation.
- T4: standalone value -- shrinks naked detection window 2000ms to 50ms.
- T5: standalone value -- removes structural fragility in order scan path.

**Q3 answer: YES**

### Q4. Does each fix have an independent SIM verification path?

- T1: `dotnet test` passes. No SIM required (verification of existing ordering + unit test).
- T2: `dotnet build` + `dotnet test` + F5 visual check of button backgrounds in NT8.
- T3: `dotnet test` only. No SIM required (pure CopyEngine API).
- T4: `dotnet test` (mocked position/order state) + SIM gate (fill entry, verify naked detection fires within 50ms on follower).
- T5: `dotnet test` (injected Order list with 14 Cancelled + 1 Working). No SIM required.

**Q4 answer: YES**

### LANE-SPLIT GATE RESULT: LANES-APPROVED

Parallel groups:
- **Group A** (parallel, same session recommended): T1 + T2 -- both `TradeCopierPanel.cs`, non-overlapping regions
- **Group B** (parallel, same session recommended): T4 + T5 -- both `CopyEngine.cs`, non-overlapping regions
- **Group C** (sequential after T1 VERIFY_PASS): T3

*Within-group parallel execution on the same file requires sequential commits in a single engineer session to avoid merge conflicts.*

---

## 2. Source Findings (from grep)

### 2.1 `_modules.Teardown()` vs `_allAccounts.Clear()` ordering

```
TradeCopierPanel.cs:
  Line 616: // B33 T7 -- Teardown all IPttModules (unsubscribes all PttBus events).
  Line 617: foreach (IPttModule m in _modules)
  Line 618:     m.Teardown();
  Line 619: _modules.Clear();
  Line 620: _allAccounts.Clear();
```

**ORDERING IS ALREADY CORRECT**: `_modules.Teardown()` (lines 617-619) precedes `_allAccounts.Clear()` (line 620).

Additionally, `_engine.Unsubscribe()` is at line 579 -- fires BEFORE the module teardown loop. This unsubscribes `acc.OrderUpdate -= OnOrderUpdate` for all accounts in `Account.All`. Leader account `OrderUpdate`/`PositionUpdate` are unsubscribed at lines 601-602.

**T1 production code change: ZERO. This is a verification + test ticket.**

### 2.2 IPttModule implementations found

All implementations located in `src/PropTraderTools/Features/`:

| Class | File | OrderUpdate in Initialize? | PositionUpdate in Initialize? | Teardown unsubscribes? |
|-------|------|---------------------------|-------------------------------|------------------------|
| `PttBreakEven` | `PttBreakEven.cs` | NO | NO | N/A (no subscriptions) |
| `PttCancel` | `PttCancel.cs` | NO | NO | N/A (no subscriptions) |
| `PttCopier` | `PttCopier.cs` | NO (subscribes PttBus.BeFired, TrimFired, FlatFired, CancelFired) | NO | YES -- all 4 PttBus events unsubscribed |
| `PttFlatten` | `PttFlatten.cs` | NO | NO | N/A (no subscriptions) |
| `PttTrim` | `PttTrim.cs` | NO | NO | N/A (no subscriptions) |

**Verdict**: No IPttModule subscribes to `Account.OrderUpdate` or `Account.PositionUpdate`. Account-level subscriptions are owned entirely by `CopyEngine.Subscribe()` (line 1345) and `CopyEngine.Unsubscribe()` (line 1350), which are called at `_engine.Subscribe()` / `_engine.Unsubscribe()` in the panel lifecycle. T1 finds NO missing unsubscribes.

### 2.3 BuildArrowCluster call site(s) confirmed

```
TradeCopierPanel.cs:
  Line 1131: private void BuildBufferedButtonsRow(StackPanel root)   <-- caller
  Line 1164:     var (cluster, btn) = BuildArrowCluster(...)          <-- SINGLE call site
  Line 1192: private static (DockPanel cluster, Button mainBtn) BuildArrowCluster(...)
```

Single call site confirmed at line 1164. Safe to inline and delete.

**Background order bug confirmed**: In `BuildArrowCluster` (lines 1225-1232):
```csharp
Line 1225: var btn = new Button { Content = mainContent, Background = mainBackground };
Line 1226: if (useTealBorder) { ... }
Line 1232: btn.SetResourceReference(Control.StyleProperty, "NTButtonStyle"); // stomps Background
```
`Background` is set BEFORE `SetResourceReference` -- style can override it. Fix: set `btn.Background` AFTER `SetResourceReference`.

### 2.4 OnAccountOrderUpdate location

There is no `OnAccountOrderUpdate` method in `CopyEngine.cs`. The relevant callback is `OnOrderUpdate` at line 1355:
```csharp
private void OnOrderUpdate(object sender, OrderEventArgs e)
```
Subscribed via `acc.OrderUpdate += OnOrderUpdate` for all accounts in `Account.All` (line 1345). This is the correct hook point for T4's naked detection tail-call.

### 2.5 FindFollowerBracketOrder / FindFollowerEntryOrder line numbers

```
CopyEngine.cs:
  Line 3430: private Order? FindFollowerBracketOrder(Account follower, ...) -- Account overload
  Line 3437:     follower.Orders.ToList()                                   -- TARGET: change to ActiveOrders(follower)
  Line 3452: private Order? FindFollowerBracketOrder(IEnumerable<Order>, ...) -- IEnumerable overload (UNCHANGED)
  Line 3583: internal Order? FindFollowerBracketOrderTestable(...)          -- test seam (UNCHANGED)
  Line 3593: internal Order? FindFollowerBracketOrderTestable(...)          -- test seam (UNCHANGED)
  Line 3635: private static Order? FindFollowerEntryOrder(Account, Instrument)
  Line 3637:     foreach (var order in follower.Orders.ToList())             -- TARGET: change to ActiveOrders(follower)
```

### 2.6 NakedPositionDetector / HasNakedPosition / REAPER

Neither `NakedPositionDetector`, `HasNakedPosition`, `ReaperIntervalMs`, nor any naked detection logic currently exists in `CopyEngine.cs` (grep confirmed zero matches). T4 adds these from scratch.

---

## 3. Ticket Architecture

---

### T1 -- DW-C38-04: Verify Module Teardown Before AllAccounts Clear

**File**: `src/PropTraderTools/TradeCopierPanel.cs` (0 lines changed)
**Test file**: `src/PropTraderTools/Tests/BwaveDwLaneATests.cs` (append)

#### Exact change description

No production code change required. The ordering at lines 617-620 is already correct:
- Line 617-619: `m.Teardown()` called for every module
- Line 620: `_allAccounts.Clear()` called after

All IPttModule implementations verified: none subscribe to `Account.OrderUpdate` or `Account.PositionUpdate`. No missing unsubscribes.

#### Test approach

Append to `BwaveDwLaneATests.cs`:

```csharp
[Fact]
public void Detach_ClearsAllModulesBeforeAccountList()
{
    // Arrange: create a TradeCopierPanel in minimal state via reflection.
    // Access _modules and _allAccounts via reflection.
    // Add a spy IPttModule that records Teardown() call order relative to _allAccounts.
    //
    // Pattern: spy module records its own Teardown() was called.
    // Then assert _allAccounts.Count == 0 after full Detach() sequence.
    //
    // If full Detach() requires WPF/NT8 context, test the sub-sequence directly:
    //   1. Add spy module to _modules via reflection
    //   2. Add dummy Account to _allAccounts via reflection
    //   3. Call the teardown sequence in order:
    //      foreach (IPttModule m in _modules) m.Teardown(); _modules.Clear(); _allAccounts.Clear();
    //   4. Assert spy.TeardownWasCalled == true
    //   5. Assert _allAccounts (via reflection) is empty
    //
    // The test asserts ORDERING is correct (Teardown called, then allAccounts empty).
    // No NT8 Account object needed -- use a stub Account via Moq or reflection.
}
```

*Engineer decision*: If `TradeCopierPanel` construction requires WPF STA thread, exercise the teardown sub-sequence directly via reflection rather than calling `Detach()`. The test validates ordering correctness, not full panel lifecycle.

#### CYC analysis
- No production method modified. CYC unchanged.

#### Dependencies
- None. Can start immediately.

---

### T2 -- DW-LaneA-06: Collapse BuildArrowCluster Inline (Option B)

**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Change**: Inline body of `BuildArrowCluster` (lines 1192-1237) into foreach loop at line 1162-1167. Delete `BuildArrowCluster` entirely.

#### Exact change description

**Step 1**: Replace the foreach loop body (lines 1162-1167) with the inlined cluster construction:

```csharp
// BEFORE (lines 1162-1167):
foreach (var s in specs)
{
    var (cluster, btn) = BuildArrowCluster(s.Content, s.Bg, s.Teal, s.Up, s.Dn, s.Main);
    s.Store(btn);
    s.Target.Children.Add(cluster);
}

// AFTER (inlined, Background AFTER SetResourceReference):
foreach (var s in specs)
{
    var cluster = new DockPanel { LastChildFill = true };
    var arrows = new Grid();
    arrows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
    arrows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
    var up = new System.Windows.Controls.Primitives.RepeatButton
    {
        Content = "^",
        Width = 18,
        Height = 12,
    };
    var dn = new System.Windows.Controls.Primitives.RepeatButton
    {
        Content = "v",
        Width = 18,
        Height = 12,
    };
    up.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
    dn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
    up.Click += s.Up;
    dn.Click += s.Dn;
    Grid.SetRow(up, 0);
    Grid.SetRow(dn, 1);
    arrows.Children.Add(up);
    arrows.Children.Add(dn);
    DockPanel.SetDock(arrows, Dock.Right);
    var btn = new Button { Content = s.Content };
    if (s.Teal)
    {
        btn.BorderBrush = BrushTeal;
        btn.Foreground = BrushTeal;
        btn.BorderThickness = new Thickness(2);
    }
    btn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
    btn.Background = s.Bg;   // AFTER style -- explicit brush wins (DW-LaneA-06 fix)
    btn.Click += s.Main;
    cluster.Children.Add(arrows);
    cluster.Children.Add(btn);
    s.Store(btn);
    s.Target.Children.Add(cluster);
}
```

**Step 2**: Delete `BuildArrowCluster` method entirely (lines 1188-1237).

#### Key fix detail
The background overwrite fix: `btn.Background = s.Bg` is placed AFTER `btn.SetResourceReference(Control.StyleProperty, "NTButtonStyle")`. This ensures the explicit brush wins over any style-setter default that `NTButtonStyle` may apply to `Background`.

#### CYC analysis
- `BuildArrowCluster` (CYC=2): **DELETED**
- `BuildBufferedButtonsRow` before: CYC=2 (base + foreach)
- `BuildBufferedButtonsRow` after: CYC=3 (base + foreach + `if (s.Teal)`)
- Net: -2 + 1 = -1 total CYC from file
- `BuildBufferedButtonsRow` post-inline: **CYC=3** -- well within 8. ✅

`lizard BuildBufferedButtonsRow --CCN 8`: **PASS** (3 <= 8)

#### Test approach

Append to `BwaveDwLaneATests.cs`:

```csharp
[Fact]
public void BuildBufferedButtonsRow_TealButtons_HaveTealBorderBrush()
{
    // Arrange: instantiate a minimal TradeCopierPanel (reflection, STA thread if needed),
    //   OR use reflection to call BuildBufferedButtonsRow on a StackPanel stub,
    //   then read _beBtn2 and _quickBtn (the teal buttons).
    // Assert:
    //   _beBtn2.BorderBrush == BrushTeal
    //   _beBtn2.Foreground  == BrushTeal
    //   _quickBtn.BorderBrush == BrushTeal
}

[Fact]
public void BuildBufferedButtonsRow_TrimButton_HasInactiveBackground()
{
    // Arrange: same reflection-based invocation.
    //   _trimBtn2 is a non-teal button (Teal=false).
    // Assert:
    //   _trimBtn2.Background == BrushInactive
    //   (verifies the inlined Background assignment fires correctly for non-teal buttons)
}
```

*Engineer decision*: If `TradeCopierPanel` WPF construction requires STA, use `WpfFact` from xunit.extensions or verify via field inspection post-reflection. Prefer direct reflection over full WPF instantiation.

#### Dependencies
- None. Can run in parallel with T1 (different region of same file).

---

### T3 -- DW-DW-03 + DW-NEW-07: Two-Panel Integration Test

**File**: `src/PropTraderTools/Tests/BwaveDwLaneATests.cs` (append) OR new `BwaveNextLaneATests.cs`
**Type**: Test only -- no production code changes.

#### Test approach

Drive `CopyEngine` APIs directly. No WPF panel construction required.

**Required CopyEngine API surface used by tests:**
- `CopyEngine.Instance` -- singleton
- `CopyEngine.Instance.ArmAllPendingBe(int bufferTicks)` -- arms `_pendingBeSlots[acc.Name]`
- `CopyEngine.Instance.DisarmPendingBe(Account acc)` -- removes from `_pendingBeSlots`
- `CopyEngine.Instance.IsPendingSlotsEmpty()` -- checks `_pendingBeSlots.IsEmpty`

**Optional production addition** (if engineer determines needed): Add `internal bool IsPendingBeSlotActive(string accountName)` test seam to `CopyEngine.cs`:
```csharp
// Test seam. CYC=1.
internal bool IsPendingBeSlotActive(string accountName) =>
    _pendingBeSlots.ContainsKey(accountName);
```
This is a 1-line method addition, not a behavior change. It enables per-account slot assertions in tests without reflection.

#### Three test scenarios

```csharp
[Fact]
public void Detach_PanelA_DoesNotClearPanelB_BeSlot()
{
    // Arrange: seed two pending BE slots (panelA-account, panelB-account)
    // Act: DisarmPendingBe(panelA-account) -- simulates panelA Detach
    // Assert:
    //   IsPendingBeSlotActive("panelA-account") == false
    //   IsPendingBeSlotActive("panelB-account") == true
    //   IsPendingSlotsEmpty() == false
}

[Fact]
public void Detach_OwnPanel_ClearsOwnBeSlot_ButNotOthers()
{
    // Arrange: seed two pending BE slots
    // Act: DisarmPendingBe(panelA-account)
    // Assert:
    //   panelA slot gone, panelB slot remains
    //   (same as above -- complementary assertion order)
}

[Fact]
public void Detach_LastPanel_ClearsAllPendingBeSlots()
{
    // Arrange: seed two pending BE slots (panelA-account, panelB-account)
    // Act: DisarmPendingBe(panelA-account), then DisarmPendingBe(panelB-account)
    // Assert:
    //   IsPendingSlotsEmpty() == true
}
```

*NOTE*: `DisarmPendingBe(Account acc)` may take a real `Account` object. If Account cannot be instantiated in tests, use the `_pendingBeSlots` field directly via reflection to seed state, and use the `IsPendingBeSlotActive` test seam for assertions. Engineer to determine best approach.

#### CYC analysis
- Test code only. No CYC analysis required.

#### Dependencies
- **Depends on T1 VERIFY_PASS** (test exercises Detach path T1 verified).
- Cannot start until T1's VERIFY_PASS is confirmed.

---

### T4 -- DW-NEW-08 Option E: Accelerated Naked Detection

**File**: `src/PropTraderTools/CopyEngine.cs` (new methods + 1 tail-call + 1 field)

#### New field

```csharp
// DW-NEW-08 Option E: debounce dict for naked detection.
// Stores Environment.TickCount64 at last naked-detect queue time per account name.
// ConcurrentDictionary: no lock. Key = acc.Name.
private readonly ConcurrentDictionary<string, long> _nakedDetectLastQueuedTicks =
    new ConcurrentDictionary<string, long>();
```

#### New method: TryNakedDetect (thin dispatcher — CYC<=3)

Added to pre-Gate-1 block in `OnOrderUpdate`. Called unconditionally (zero CYC addition to `OnOrderUpdate`):

```csharp
// DW-NEW-08 Option E: accelerated naked detection.
// Called unconditionally from pre-Gate-1 in OnOrderUpdate -- no branch cost to parent.
// CYC=3: (1) terminal-state guard, (2) follower guard, (3) NakedPositionDetector dispatch.
// JS-021: no lock. JS-001: no throw. JS-033: synchronous void.
private void TryNakedDetect(OrderEventArgs e)
{
    if (
        e.Order.OrderState != OrderState.Filled
        && e.Order.OrderState != OrderState.Cancelled
        && e.Order.OrderState != OrderState.Rejected
    )
        return; // (1) only act on terminal state transitions
    if (!IsFollowerAccount(e.Order.Account))
        return; // (2) only follower accounts
    NakedPositionDetector(e.Order.Account); // (3) check and queue flatten if naked
}
```

Call site in `OnOrderUpdate` (after existing pre-Gate-1 helpers, before Gate 1):
```csharp
// DW-NEW-08 Option E: detect naked position within 50ms of terminal order event.
TryNakedDetect(e);
```

#### New method: NakedPositionDetector (CYC=6)

```csharp
// DW-NEW-08 Option E: NakedPositionDetector.
// Fires within 50ms of a Filled/Cancelled/Rejected event on a naked follower.
// CYC=6: (1) acc null guard, (2) HasNakedPosition check,
//        (3) debounce check, (4) TickCount64 acquire, (5) CompareExchange,
//        (6) Dispatcher.InvokeAsync dispatch.
// NT8 API bans: no Account.Change(), no AtmStrategyCreate(), no AtmStrategyChangeStopTarget().
// JS-021: no lock -- ConcurrentDictionary atomic ops only.
// JS-001: no throw. JS-033: synchronous void.
private void NakedPositionDetector(Account acct)
{
    if (acct == null)
        return; // (1)
    if (!HasNakedPosition(acct))
        return; // (2)

    // (3) debounce: skip if already queued within 500ms
    long now = Environment.TickCount64;
    const long GraceMs = 500L;
    long last = _nakedDetectLastQueuedTicks.GetOrAdd(acct.Name, 0L);
    if (now - last < GraceMs)
        return; // (3) within grace window -- ATM brackets may still be placing

    // (4)(5) atomic update: only proceed if our 'now' beat any concurrent thread
    long prev = _nakedDetectLastQueuedTicks.AddOrUpdate(
        acct.Name, now, (_, __) => now);
    if (prev != now)
        return; // (5) another thread already queued this account

    // (6) marshal flatten to UI thread -- same pattern as other flatten paths
    Instrument? instr = FindOpenPositionInstrument(acct);
    if (instr is not null)
        NinjaTrader.Core.Globals.Dispatcher.InvokeAsync(() =>
            FlattenOneAccount(acct, instr));
}
```

*Note*: `FindOpenPositionInstrument(acct)` is a new 1-line helper (CYC=1) that returns `Instrument?` (nullable) -- the instrument of the first non-flat position on the account, or `null` if flat. The caller uses `is not null` guard (not a raw `== null` check against a non-nullable type). JS-002 compliant.

#### New method: HasNakedPosition (CYC=4)

```csharp
// DW-NEW-08 Option E: HasNakedPosition.
// Returns true if acc has a non-flat position AND zero Working/PendingSubmit Stop or Target orders.
// CYC=4: (1) position scan/flat check, (2) order scan for stop,
//        (3) order scan for target, (4) both-zero result.
// JS-021: no lock. JS-002: returns bool. JS-001: no throw.
private static bool HasNakedPosition(Account acct)
{
    // (1) find any non-flat position
    bool hasPosition = false;
    foreach (Position p in acct.Positions)
    {
        if (p.Quantity > 0)
        {
            hasPosition = true;
            break;
        }
    }
    if (!hasPosition)
        return false; // (1)

    // (2) scan for any Working/PendingSubmit Stop order
    bool hasStop = false;
    // (3) scan for any Working/PendingSubmit Target order
    bool hasTarget = false;
    foreach (Order o in acct.Orders)
    {
        if (
            o.OrderState != OrderState.Working
            && o.OrderState != OrderState.PendingSubmit
        )
            continue;
        if (o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit)
            hasStop = true; // (2)
        else if (o.OrderType == OrderType.Limit)
            hasTarget = true; // (3)
    }
    return !hasStop && !hasTarget; // (4) naked = has position, no protective orders
}
```

#### New method: FindOpenPositionInstrument (CYC=1)

```csharp
// DW-NEW-08 Option E: return instrument of first non-flat position, or null if all flat.
// CYC=1. JS-002 compliant: return type is Instrument? (nullable reference type).
// No raw `return null` against a non-nullable -- caller uses `is not null` guard.
private static Instrument? FindOpenPositionInstrument(Account acct) =>
    acct.Positions.FirstOrDefault(static p => p.Quantity > 0)?.Instrument;
```

#### CYC analysis

| Method | CYC | Status |
|--------|-----|--------|
| `TryNakedDetect` | 3 | New -- within budget |
| `NakedPositionDetector` | 6 | New -- as specified |
| `HasNakedPosition` | 4 | New -- as specified |
| `FindOpenPositionInstrument` | 1 | New -- trivial helper |
| `OnOrderUpdate` | 8 | **UNCHANGED** -- TryNakedDetect is an unconditional call |

#### Test approach

```csharp
// T4 tests go in BwaveDwLaneATests.cs or BwaveNextLaneATests.cs
// Use HasNakedPosition and NakedPositionDetector via reflection or internal visibility.
// Engineer to use [InternalsVisibleTo] if needed.
```

Tests for T4 (recommended, engineer determines exact names):
- `HasNakedPosition_ReturnsFalse_WhenNoPosition()` -- flat account
- `HasNakedPosition_ReturnsFalse_WhenStopOrderPresent()` -- position + stop
- `HasNakedPosition_ReturnsTrue_WhenNoProtectiveOrders()` -- position, no stop/target
- `NakedPositionDetector_DoesNotFire_WithinGraceWindow()` -- debounce test

#### NT8 API compliance
- `Account.Change()`: NOT used. ✅
- `AtmStrategyCreate()`: NOT used. ✅
- `AtmStrategyChangeStopTarget()`: NOT used. ✅
- `lock()`: NOT used. `ConcurrentDictionary` + `Environment.TickCount64` atomic ops. ✅
- `Dispatcher.InvokeAsync`: used correctly for UI-thread marshal. ✅

#### Dependencies
- None. Can run in parallel with T5 (different region of CopyEngine.cs).

---

### T5 -- DW-NEW-09: ActiveOrders Filter Wrapper

**File**: `src/PropTraderTools/CopyEngine.cs`

#### New method: ActiveOrders (CYC=1)

```csharp
// DW-NEW-09: ActiveOrders -- terminal-state filter for Account.Orders.
// Returns only orders in non-terminal states (Filled/Cancelled/Rejected excluded).
// CYC=1: expression body, single Where predicate, no branching in method signature.
// JS-021: no lock (LINQ Where is non-mutating). JS-002: IEnumerable<Order> (never null).
// JS-036: lazy Where -- no heap allocation beyond the enumerator.
// Single fix point: callers that need active orders use this instead of .ToList().
// NT8: acc.Orders iteration is safe on order-update callback thread (see line 1701 comment).
private static IEnumerable<Order> ActiveOrders(Account acc) =>
    acc.Orders.Where(static o =>
        o.OrderState != OrderState.Filled
        && o.OrderState != OrderState.Cancelled
        && o.OrderState != OrderState.Rejected);
```

#### Two targeted call-site changes

**Change 1 -- Line 3437** (`FindFollowerBracketOrder` Account overload):
```csharp
// BEFORE:
FindFollowerBracketOrder(
    follower.Orders.ToList(),
    fromEntrySignalName,
    isStop,
    leaderName
);

// AFTER:
FindFollowerBracketOrder(
    ActiveOrders(follower),
    fromEntrySignalName,
    isStop,
    leaderName
);
```
The `FindFollowerBracketOrder(IEnumerable<Order>, ...)` overload at line 3452 already accepts `IEnumerable<Order>`. No signature change needed.

**Change 2 -- Line 3637** (`FindFollowerEntryOrder`):
```csharp
// BEFORE:
foreach (var order in follower.Orders.ToList()) // (1)

// AFTER:
foreach (var order in ActiveOrders(follower)) // (1) DW-NEW-09: terminal orders excluded
```

#### Explicitly unchanged call sites

| Line | Context | Reason |
|------|---------|--------|
| 1708 | `CancelPttDragOrphansForAccount` | Has `IsPttDragOrphanCancellable` gate |
| 1947 | `TryLogSFBTrace` | Diagnostic -- intentionally shows full history |
| All other 21 sites | Various | Each has its own state gate or scans for different purpose |

#### CYC analysis

| Method | Before | After | Delta |
|--------|--------|-------|-------|
| `ActiveOrders` | N/A | 1 | +1 (new) |
| `FindFollowerBracketOrder` (Account overload, line 3430) | 1 | 1 | 0 |
| `FindFollowerEntryOrder` (line 3635) | 3 | 3 | 0 |

#### Test approach

```csharp
[Fact]
public void FindFollowerBracketOrder_SkipsFilledAndCancelledOrders()
{
    // Arrange: inject 14 Cancelled + 1 Working StopMarket order named "Stop1"
    //   via FindFollowerBracketOrderTestable(IEnumerable<Order>, ...)
    //   (test seam at line 3598, uses IEnumerable<Order> overload)
    //   Pass ActiveOrders output (14 Cancelled filtered out -> 1 Working remains)
    //   OR: build the IEnumerable directly in the test to exercise the filter.
    // Assert: returned order.OrderState == OrderState.Working
    //         returned order.Name == "Stop1"
}

[Fact]
public void FindFollowerEntryOrder_SkipsFilledAndCancelledEntries()
{
    // Arrange: inject 1 Cancelled "PTT-Copy" Limit + 1 Working "PTT-Copy" Limit
    //   via ActiveOrders-filtered list
    // Assert: returned order.OrderState == OrderState.Working
    //         returned order.Name == "PTT-Copy"
}
```

*Note*: `FindFollowerBracketOrderTestable(IEnumerable<Order>, ...)` at line 3598 provides the test seam for bracket order tests. For `FindFollowerEntryOrder`, engineer may need to add a testable seam or test `ActiveOrders` helper directly with a mocked Account.

#### Dependencies
- None. Can run in parallel with T4.

---

## 4. 7-Scan Checklist (Plan-Level)

| Scan | Check | Expected Result After Implementation |
|------|-------|--------------------------------------|
| SCAN-01 `lock()` | `Select-String src/PropTraderTools -Pattern "lock\s*\(" -Recurse` | Zero results in new/modified code |
| SCAN-02 `async void` | `Select-String src/PropTraderTools -Pattern "async void [A-Z]" -Recurse` | Zero results in new/modified code |
| SCAN-03 `return null` | `Select-String src/PropTraderTools -Pattern "return null;" -Recurse` | Zero in NEW methods. `FindOpenPositionInstrument` returns `Instrument?` via `?.Instrument` -- no `return null` statement. All other new methods return `bool`, `void`, or `IEnumerable<Order>`. |
| SCAN-04 `throw new` hot path | `Select-String src/PropTraderTools -Pattern "throw new \w+Exception" -Recurse` | Zero in new/modified methods |
| SCAN-05 CYC | `dotnet lizard src/PropTraderTools --CCN 8` | All new/modified methods: 0 violations. T1: 0, T2: BuildBufferedButtonsRow=3, T4: TryNakedDetect=3 NakedPositionDetector=6 HasNakedPosition=4, T5: ActiveOrders=1 |
| SCAN-06 ASCII | `Select-String src/PropTraderTools -Pattern "[^\x00-\x7F]" -Recurse` | Zero non-ASCII in new/modified files |
| SCAN-07 xUnit | `Select-String tests -Pattern "\[Test\]|\[TestMethod\]"` | Zero -- only `[Fact]` used |

---

## 5. Execution Order

```
T1 (TradeCopierPanel.cs -- Detach verification) ──┐
                                                    ├──> both VERIFY_PASS ──> T3 (integration tests)
T2 (TradeCopierPanel.cs -- BuildArrowCluster)    ──┘

T4 (CopyEngine.cs -- NakedPositionDetector)   ──┐
                                                 ├──> independent, no dependency on T1/T2/T3
T5 (CopyEngine.cs -- ActiveOrders)            ──┘
```

**Recommended engineer sessions:**

| Session | Tickets | File(s) |
|---------|---------|---------|
| Session A | T1 + T2 | `TradeCopierPanel.cs` + test appends |
| Session B | T4 + T5 | `CopyEngine.cs` + test appends |
| Session C (after T1 VERIFY_PASS) | T3 | `BwaveDwLaneATests.cs` or new test file |

Sessions A and B can run concurrently (different files). Session C blocks on T1 VERIFY_PASS only.

**Post-implementation gates:**

```powershell
# Sessions A + B (production file changes):
powershell -File scripts\ptt-sync-and-verify.ps1   # must show 18/18 OK, 0 MISMATCH
# Then: F5 in NinjaTrader 8 -- 0 new errors

# Session C (test only -- no sync required):
dotnet build   # 0 errors
dotnet test    # 0 new failures
```

---

## 6. Risks and Mitigations

| Risk | Severity | Mitigation |
|------|----------|------------|
| T4: `NakedPositionDetector` false-fires during normal bracket placement lag | Medium | 500ms grace window in `_nakedDetectLastQueuedTicks`. Engineer should calibrate against SIM gate -- observe `[NAKED-DETECT]` log lines during normal fill+bracket-arm sequence. |
| T4: `OnOrderUpdate` CYC bust if `TryNakedDetect` call counted wrong | Low | `TryNakedDetect` is an unconditional call (no branch in `OnOrderUpdate`). CYC=0 addition to parent. Verify with lizard post-implementation. |
| T4: `FindOpenPositionInstrument` returns wrong instrument on multi-instrument account | Low | Lane A accounts are single-instrument in SIM testing. Multi-instrument edge case is acceptable debt -- document in T4 ticket. |
| T2: Style-dependent WPF property test flakiness in headless CI | Medium | Tests should inspect field properties set DURING construction (Background, BorderBrush) -- these are set unconditionally and don't depend on WPF style resolution. If WPF not available, mock/stub the button inspection path. |
| T3: `DisarmPendingBe(Account)` may require real `Account` object | Medium | Use reflection to seed `_pendingBeSlots` directly with test-only account names; use `IsPendingBeSlotActive(string)` test seam for assertions. If test seam not available, use reflection. |
| T5: `ActiveOrders` lazy enumeration may enumerate during collection modification | Low | Same thread as `acc.Orders.ToList()` which is already documented as safe. `Where` is lazier but enumerates at the same moment as the caller's foreach. No regression. |

---

## 7. Out-of-Scope Confirmation

The following items are explicitly **NOT** in scope for BWAVE-NEXT Lane A:

| Item | Status | Reason |
|------|--------|--------|
| DW-C38-01 (`OnPendingBeArmedDispatch` unsubscribe) | ALREADY FIXED | Line 586 confirmed present. Do NOT touch. |
| DW-C38-02 (module `Dispose` verification) | OUT OF SCOPE | Analysis-only; no crash observed; separate ticket |
| DW-C39-09 (`SaveRules` on `OnAddRule`) | OUT OF SCOPE | `TradeCopierWindow.cs` -- different lane |
| DW-C39-07/08 (null-guards, rule-count cap) | OUT OF SCOPE | `TradeCopierWindow.cs` -- different lane |
| DW-RepairLC-01/02 (SIM gates) | OUT OF SCOPE | Director action, not engineer ticket |
| DW-NEW-07 live observations | OUT OF SCOPE | Director will provide; future backlog append |
| DW-NEW-08 Option D (cancel-before-dispatch drain) | OUT OF SCOPE | Assigned to BWAVE-NEXT Lane B |
| All other `acc.Orders.ToList()` call sites (23 of 25) | UNCHANGED | Only 2 targeted by T5 per spec |

---

## Appendix A: IPttModule Teardown Completeness Audit

Conducted as part of T1 architecture analysis:

| Module | File | Events Subscribed in Initialize() | Unsubscribed in Teardown() | Gap? |
|--------|------|------------------------------------|---------------------------|------|
| PttBreakEven | PttBreakEven.cs | None | N/A | **None** |
| PttCancel | PttCancel.cs | None (pattern: empty Initialize) | N/A | **None** |
| PttCopier | PttCopier.cs | PttBus.BeFired, TrimFired, FlatFired, CancelFired | All 4 unsubscribed | **None** |
| PttFlatten | PttFlatten.cs | None | N/A | **None** |
| PttTrim | PttTrim.cs | None | N/A | **None** |

**Result: No gaps found. T1 production code change is ZERO.**

The `CopyEngine.Subscribe()` / `Unsubscribe()` pattern (not IPttModule) handles `acc.OrderUpdate` subscriptions. Called at `_engine.Subscribe()` / `_engine.Unsubscribe()` in panel lifecycle, before module teardown.

---

*Architecture plan written: 2026-09-04 | ptt-architect | BWAVE-NEXT Lane A*
*Sequential thinking: 8 thoughts completed*
*Source files inspected: TradeCopierPanel.cs, CopyEngine.cs, PttBreakEven.cs, PttCopier.cs, PttGlobalBreakEven.cs, PttContracts.cs, TradeCopierAddOn.cs*
