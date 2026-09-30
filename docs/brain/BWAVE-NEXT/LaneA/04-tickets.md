# BWAVE-NEXT Lane A -- Tickets

**Status**: TICKETS_COMPLETE
**Author**: ptt-architect
**Date**: 2026-09-04
**Source plan**: `docs/brain/BWAVE-NEXT/LaneA/02-architecture-plan.md` (REVIEW_PASS)
**Reviewer confirmation**: `docs/brain/BWAVE-NEXT/LaneA/02-plan-review.md` (REVIEW_PASS cycle 2)

---

## Execution Order

```
PARALLEL GROUP A (same engineer session, sequential commits):
  Ticket 1 (TradeCopierPanel.cs -- teardown region, lines 616-620) -- TEST ONLY
  Ticket 2 (TradeCopierPanel.cs -- button building region, lines 1131-1237) -- PRODUCTION

PARALLEL GROUP B (same engineer session, sequential commits, independent of Group A):
  Ticket 4 (CopyEngine.cs -- OnOrderUpdate tail-call + new REAPER methods) -- PRODUCTION
  Ticket 5 (CopyEngine.cs -- ActiveOrders wrapper + 2 call-site changes) -- PRODUCTION

SEQUENTIAL (after T1 VERIFY_PASS only):
  Ticket 3 (test file -- exercises T1 production code) -- TEST ONLY

Ph5 FINAL REVIEW: only after all 5 tickets reach VERIFY_PASS
```

Sessions A and B can run concurrently (different source files). Session C blocks on T1 VERIFY_PASS only.

---

## Ticket 1 -- DW-C38-04: Verify Module Teardown Ordering

**File(s)**: `src/PropTraderTools/Tests/BwaveDwLaneATests.cs` (append to existing)
**Type**: Test Only (production code change = ZERO -- ordering already correct)
**Spec Req IDs**: DW-C38-04
**Dependencies**: None -- can start immediately, parallel with T2

### Change Description

The architect confirmed in plan §2.1 that `_modules.Teardown()` at lines 617-619 already
precedes `_allAccounts.Clear()` at line 620 in `TradeCopierPanel.Detach()`. No production
code change is required.

Engineer verification steps (run before writing the test):

1. Grep to confirm teardown ordering:
   ```powershell
   Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "_modules|_allAccounts" | Select-Object LineNumber, Line
   ```
   Expected: `_modules.Teardown()` at line 617-619, `_allAccounts.Clear()` at line 620.

2. Grep all IPttModule implementations for account-level subscriptions:
   ```powershell
   Select-String -Path src/PropTraderTools/Features/*.cs -Pattern "OrderUpdate \+=|PositionUpdate \+="
   ```
   Expected: ZERO results (plan §2.2 confirms no module subscribes to Account.OrderUpdate or Account.PositionUpdate).

3. If any module IS subscribing to `OrderUpdate`/`PositionUpdate`: add the matching `-=` unsubscribe
   in **that module's `Teardown()` method** -- not in `TradeCopierPanel.Detach()`. Each module owns its subscriptions.

4. If ordering confirmed correct and zero subscription gaps found: write test only (no production code change).

### Method Signatures (new/modified)

No production methods added or modified. Test method only:

```csharp
// In BwaveDwLaneATests.cs
[Fact]
public void Detach_ClearsAllModulesBeforeAccountList()
```

### CYC Analysis

No production method modified. CYC: unchanged.
Test method CYC=1 (no branches). `lizard` will not flag test methods.

### Acceptance Criteria

1. `_modules.Teardown()` confirmed at line 617-619 (before `_allAccounts.Clear()` at line 620)
2. All 5 IPttModule implementations confirmed: ZERO subscriptions to `Account.OrderUpdate` or `Account.PositionUpdate` -- no missing unsubscribes
3. If any missing unsubscribe found: added in the module's own `Teardown()`, not in `TradeCopierPanel`
4. No `lock()` introduced
5. `[Fact] Detach_ClearsAllModulesBeforeAccountList()` passes without `[Skip]`
6. Test verifies: after simulated teardown sequence, all modules have been torn down AND `_allAccounts` is empty

### Test Coverage

```csharp
[Fact]
public void Detach_ClearsAllModulesBeforeAccountList()
{
    // Arrange:
    //   Use reflection to access TradeCopierPanel private fields _modules and _allAccounts.
    //   If TradeCopierPanel construction requires WPF STA thread, do NOT construct the full panel.
    //   Instead: exercise the teardown sub-sequence directly:
    //     1. Create a spy IPttModule (Moq or hand-rolled stub) that records Teardown() invocation.
    //     2. Add spy to _modules via reflection.
    //     3. Add a dummy object/null to _allAccounts via reflection (simulates a tracked account).
    //
    // Act: execute teardown sub-sequence in order:
    //   foreach (IPttModule m in _modules) m.Teardown();
    //   _modules.Clear();
    //   _allAccounts.Clear();
    //
    // Assert:
    //   spyModule.TeardownWasCalled == true  (module teardown fired)
    //   _allAccounts (via reflection).Count == 0  (accounts cleared after teardown)
    //
    // The test validates ORDERING: teardown was called, then the list is empty.
    // No real NT8 Account object is needed -- any stub/null entry in _allAccounts is sufficient.
}
```

**Engineer decision**: If `TradeCopierPanel` requires WPF STA for any field initialization,
use `[WpfFact]` from `Xunit.Extensions.Ordering` OR exercise the sub-sequence directly
via reflection rather than calling `Detach()` on a fully constructed panel.

### NT8 Sync Requirement

**NOT REQUIRED.** This ticket adds no production `.cs` changes. Test file only.
Run `dotnet build && dotnet test` to verify.

### 7-Scan Checklist

| Scan | Command | Expected Result |
|------|---------|----------------|
| SCAN-01 JS-021 lock() | `grep -r "lock\s*(" src/PropTraderTools --include="*.cs"` | 0 results in new/modified code |
| SCAN-02 JS-033 async void | `grep -r "async void [A-Z]" src/PropTraderTools --include="*.cs"` | 0 results in new/modified code |
| SCAN-03 JS-002 return null | `grep -n "return null" src/PropTraderTools/Tests/BwaveDwLaneATests.cs` | 0 new occurrences |
| SCAN-04 JS-001 throw | `grep -n "throw new" src/PropTraderTools/Tests/BwaveDwLaneATests.cs` | 0 new exceptions |
| SCAN-05 CYC<=8 | `lizard src/PropTraderTools/Tests/BwaveDwLaneATests.cs --CCN 8` | 0 warnings (test method CYC=1) |
| SCAN-06 ASCII | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/Tests/BwaveDwLaneATests.cs` | 0 results |
| SCAN-07 xUnit | `grep -n "\[Fact\]" src/PropTraderTools/Tests/BwaveDwLaneATests.cs` | all [Fact], never [Test] |

---

## Ticket 2 -- DW-LaneA-06: Collapse BuildArrowCluster Inline

**File(s)**: `src/PropTraderTools/TradeCopierPanel.cs`
**Type**: Production Fix
**Spec Req IDs**: DW-LaneA-06
**Dependencies**: None -- can start immediately, parallel with T1

### Change Description

Single call site confirmed at line 1164: `var (cluster, btn) = BuildArrowCluster(...)`.
`BuildArrowCluster` is defined at lines 1192-1237. It is called from exactly one place.

**Background overwrite bug**: In `BuildArrowCluster`, `btn.Background = mainBackground` is set at
line 1225 BEFORE `btn.SetResourceReference(Control.StyleProperty, "NTButtonStyle")` at line 1232.
The `NTButtonStyle` WPF resource may override `Background` via a style setter. Fix: set
`btn.Background` AFTER `SetResourceReference`.

**Step 1** -- Replace the foreach loop body (lines 1162-1167) with the inlined construction:

```csharp
// BEFORE (lines 1162-1167):
foreach (var s in specs)
{
    var (cluster, btn) = BuildArrowCluster(s.Content, s.Bg, s.Teal, s.Up, s.Dn, s.Main);
    s.Store(btn);
    s.Target.Children.Add(cluster);
}

// AFTER (inlined -- Background set AFTER SetResourceReference per DW-LaneA-06):
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

**Step 2** -- Delete `BuildArrowCluster` method entirely (lines 1192-1237). No other callers exist.

**Step 3** -- Verify `dotnet build` produces 0 errors after deletion.

**Critical detail**: The inlined block preserves the `useTealBorder` conditional (now `if (s.Teal)`)
and all `SetResourceReference` calls. Do NOT change the specs array layout.

### Method Signatures (new/modified)

```csharp
// DELETED (was lines 1192-1237):
// private static (DockPanel cluster, Button mainBtn) BuildArrowCluster(
//     object mainContent, Brush mainBackground, bool useTealBorder,
//     RoutedEventHandler upHandler, RoutedEventHandler dnHandler, RoutedEventHandler mainHandler)

// MODIFIED (lines 1131-1187 region, inlined body):
// private void BuildBufferedButtonsRow(StackPanel root)
// Signature UNCHANGED. CYC changes from 2 to 3.
```

### CYC Analysis

| Method | Before | After | Lizard Expected |
|--------|--------|-------|----------------|
| `BuildArrowCluster` | 2 | DELETED | N/A |
| `BuildBufferedButtonsRow` | 2 | 3 | 0 warnings (3 <= 8) |

Net CYC delta for file: -2 (deleted) +1 (absorbed branch) = -1 total. ✓

### Acceptance Criteria

1. `BuildArrowCluster` method deleted entirely -- no remaining definition or call site
2. `BuildBufferedButtonsRow` inlines the cluster construction -- all 6 button specs (Trim, Flatten, BE, BE ALL, Quick, Quick ALL) build without error
3. Teal buttons (BE, BE ALL, Quick, Quick ALL): retain `BrushTeal` border + foreground
4. `btn.Background = s.Bg` is set **AFTER** `btn.SetResourceReference(Control.StyleProperty, "NTButtonStyle")` in the inlined code
5. `dotnet build` 0 errors, 0 warnings
6. `lizard BuildBufferedButtonsRow --CCN 8`: 0 warnings (CYC=3)
7. No `lock()`, no `async void`, no `return null`, ASCII-only
8. F5 in NinjaTrader 8: 0 new errors, buttons visible and functional
9. `[Fact] BuildBufferedButtonsRow_TealButtons_HaveTealBorderBrush()` passes
10. `[Fact] BuildBufferedButtonsRow_TrimButton_HasInactiveBackground()` passes

### Test Coverage

```csharp
[Fact]
public void BuildBufferedButtonsRow_TealButtons_HaveTealBorderBrush()
{
    // Arrange:
    //   Instantiate a minimal TradeCopierPanel or use reflection to call BuildBufferedButtonsRow
    //   on a StackPanel stub (STA thread if WPF required).
    //   Read _beBtn2 and _quickBtn fields via reflection after the call.
    // Assert:
    //   _beBtn2.BorderBrush == BrushTeal  (teal button has teal border)
    //   _beBtn2.Foreground  == BrushTeal  (teal button has teal foreground)
    //   _quickBtn.BorderBrush == BrushTeal (Quick button is also teal)
    //
    // Engineer note: BrushTeal is a static field on TradeCopierPanel.
    // Use reflection to obtain its value for comparison.
}

[Fact]
public void BuildBufferedButtonsRow_TrimButton_HasInactiveBackground()
{
    // Arrange: same reflection-based invocation.
    //   _trimBtn2 is a non-teal button (s.Teal = false in its spec entry).
    // Assert:
    //   _trimBtn2.Background == BrushInactive
    //   (verifies the inlined Background assignment fires correctly for non-teal buttons)
    //
    // This test specifically validates the DW-LaneA-06 fix:
    // Background is set after SetResourceReference so the explicit brush wins.
}
```

**Engineer decision**: If WPF construction requires STA, use `[WpfFact]` from
`Xunit.Extensions.Ordering` or inspect field properties set unconditionally during
construction via reflection. Background, BorderBrush are set unconditionally -- they do not
depend on WPF style resolution completing, so reflection-based property checks are reliable.

### NT8 Sync Requirement

**REQUIRED.** Run after all T2 changes are complete:
```powershell
powershell -File scripts\ptt-sync-and-verify.ps1
```
Expected: `18/18 OK, 0 MISMATCH`.
Record the output verbatim in `ticket-2-completion.md`.
Then press **F5** in NinjaTrader 8 to recompile. Confirm 0 new errors.

### 7-Scan Checklist

| Scan | Command | Expected Result |
|------|---------|----------------|
| SCAN-01 JS-021 lock() | `grep -r "lock\s*(" src/PropTraderTools --include="*.cs"` | 0 results in new/modified code |
| SCAN-02 JS-033 async void | `grep -r "async void [A-Z]" src/PropTraderTools --include="*.cs"` | 0 results in new/modified code |
| SCAN-03 JS-002 return null | `grep -n "return null" src/PropTraderTools/TradeCopierPanel.cs` | 0 new occurrences in modified region |
| SCAN-04 JS-001 throw | `grep -n "throw new" src/PropTraderTools/TradeCopierPanel.cs` | 0 new exceptions in modified region |
| SCAN-05 CYC<=8 | `lizard src/PropTraderTools/TradeCopierPanel.cs --CCN 8` | BuildBufferedButtonsRow: 0 warnings (CYC=3) |
| SCAN-06 ASCII | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/TradeCopierPanel.cs` | 0 results |
| SCAN-07 xUnit | `grep -n "\[Fact\]" src/PropTraderTools/Tests/BwaveDwLaneATests.cs` | all [Fact], never [Test] |

---

## Ticket 3 -- DW-DW-03 + DW-NEW-07: Two-Panel BE Integration Test

**File(s)**: `src/PropTraderTools/Tests/BwaveDwLaneATests.cs` (append) OR new `BwaveNextLaneATests.cs`
**Type**: Test Only (optional: 1-line test seam added to `CopyEngine.cs`)
**Spec Req IDs**: DW-DW-03, DW-NEW-07
**Dependencies**: **T1 VERIFY_PASS required before this ticket starts**

### Change Description

CopyEngine API-driven tests. No WPF panel construction required.

**Engineer pre-work (investigate before coding)**:

1. Grep for the pending BE slot API surface in CopyEngine.cs:
   ```powershell
   Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "ArmPendingBe|DisarmPendingBe|IsPendingBeSlot|PendingBeSlot|_pendingBeSlot" | Select-Object LineNumber, Line
   ```
   Identify the exact method names and signatures for arming/disarming BE slots and checking slot state.

2. If no `IsPendingBeSlotActive(string accountName)` test seam exists, add it to `CopyEngine.cs`:
   ```csharp
   // Test seam only. CYC=1.
   // JS-021: no lock. JS-002: returns bool.
   internal bool IsPendingBeSlotActive(string accountName) =>
       _pendingBeSlots.ContainsKey(accountName);
   ```
   This is a 1-line method addition -- not a behavior change. If added, the NT8 sync requirement
   applies (see below).

3. Seed `_pendingBeSlots` state by calling the existing arming API or using reflection on
   `_pendingBeSlots` directly if the arming API requires a real `Account` object.

**Three test scenarios (S1, S2, S3)**:

- **S1 sibling isolation**: arm two slots, disarm slot A, assert slot B still armed and `IsPendingSlotsEmpty() == false`
- **S2 own-account cleanup**: arm one slot, disarm it, assert `IsPendingSlotsEmpty() == true`
- **S3 last-panel global cleanup**: arm two slots, disarm A then B, assert `IsPendingSlotsEmpty() == true`

### Method Signatures (new/modified)

```csharp
// Optional test seam in CopyEngine.cs (only if it does not already exist):
// File: src/PropTraderTools/CopyEngine.cs
internal bool IsPendingBeSlotActive(string accountName) =>
    _pendingBeSlots.ContainsKey(accountName);
// CYC=1. JS-021: no lock (ConcurrentDictionary.ContainsKey is lock-free).
// JS-002: returns bool. JS-033: synchronous.

// Test methods in BwaveDwLaneATests.cs:
[Fact] public void Detach_PanelA_DoesNotClearPanelB_BeSlot()
[Fact] public void Detach_LastPanel_ClearsAllPendingBeSlots()
[Fact] public void Detach_OwnPanel_ClearsOwnBeSlot_ButNotOthers()
```

### CYC Analysis

| Method | CYC | Notes |
|--------|-----|-------|
| `IsPendingBeSlotActive` (optional seam) | 1 | Expression body, no branches |
| Each [Fact] test method | 1 | No branches in test setup |

### Acceptance Criteria

1. All 3 `[Fact]` methods pass without `[Skip]`
2. S1: Detach of panel A does NOT clear panel B's BE slot -- `IsPendingBeSlotActive("panelB-account") == true` after disarming panel A
3. S2: Detach of own panel clears own slot -- `IsPendingBeSlotActive("panelA-account") == false` after disarming
4. S3: Detach of both panels clears all slots -- `IsPendingSlotsEmpty() == true` after both disarmed
5. No `[Skip]`, no `WpfFact` required (CopyEngine driven directly, no WPF)
6. No `lock()`, xUnit-only `[Fact]`, ASCII-only
7. Test file appended to `BwaveDwLaneATests.cs` OR added as new `BwaveNextLaneATests.cs` (engineer choice based on file size)
8. If new test file: add `<Compile Include="Tests/BwaveNextLaneATests.cs" />` entry to `PropTraderTools.csproj`

### Test Coverage

```csharp
[Fact]
public void Detach_PanelA_DoesNotClearPanelB_BeSlot()
{
    // Arrange:
    //   Seed two pending BE slots:
    //     CopyEngine.Instance._pendingBeSlots["panelA-account"] via reflection or ArmPendingBe API
    //     CopyEngine.Instance._pendingBeSlots["panelB-account"] via reflection or ArmPendingBe API
    //
    // Act: DisarmPendingBe("panelA-account") -- simulates panel A Detach
    //
    // Assert:
    //   IsPendingBeSlotActive("panelA-account") == false  (own slot cleared)
    //   IsPendingBeSlotActive("panelB-account") == true   (sibling slot untouched)
    //   IsPendingSlotsEmpty() == false                    (not all slots cleared)
}

[Fact]
public void Detach_OwnPanel_ClearsOwnBeSlot_ButNotOthers()
{
    // Arrange: seed two pending BE slots (complementary assertions to S1)
    //
    // Act: DisarmPendingBe("panelA-account")
    //
    // Assert:
    //   panelA slot gone (IsPendingBeSlotActive("panelA-account") == false)
    //   panelB slot remains (IsPendingBeSlotActive("panelB-account") == true)
}

[Fact]
public void Detach_LastPanel_ClearsAllPendingBeSlots()
{
    // Arrange: seed two pending BE slots (panelA-account, panelB-account)
    //
    // Act:
    //   DisarmPendingBe("panelA-account")  -- first panel closes
    //   DisarmPendingBe("panelB-account")  -- second (last) panel closes
    //
    // Assert:
    //   IsPendingSlotsEmpty() == true  (all pending BE slots cleared)
}
```

**Engineer note**: `DisarmPendingBe` may take a real `Account` object. If `Account` cannot be
instantiated in tests, seed `_pendingBeSlots` directly via reflection with test account name strings
and use `IsPendingBeSlotActive(string)` test seam for assertions.
Each test should reset `_pendingBeSlots` state (clear before seeding) to ensure isolation.

### NT8 Sync Requirement

**Conditional**: If the optional `IsPendingBeSlotActive` test seam is added to `CopyEngine.cs`,
run after all T3 changes are complete:
```powershell
powershell -File scripts\ptt-sync-and-verify.ps1
```
Expected: `18/18 OK, 0 MISMATCH`.
Record output verbatim in `ticket-3-completion.md`.
Then press **F5** in NinjaTrader 8. Confirm 0 new errors.

If NO production code was added (tests only): run `dotnet build && dotnet test` only. No sync needed.

### 7-Scan Checklist

| Scan | Command | Expected Result |
|------|---------|----------------|
| SCAN-01 JS-021 lock() | `grep -r "lock\s*(" src/PropTraderTools --include="*.cs"` | 0 results in new/modified code |
| SCAN-02 JS-033 async void | `grep -r "async void [A-Z]" src/PropTraderTools --include="*.cs"` | 0 results in new/modified code |
| SCAN-03 JS-002 return null | `grep -n "return null" src/PropTraderTools/CopyEngine.cs` | 0 new occurrences (seam returns bool) |
| SCAN-04 JS-001 throw | `grep -n "throw new" src/PropTraderTools/Tests/BwaveDwLaneATests.cs` | 0 new exceptions |
| SCAN-05 CYC<=8 | `lizard src/PropTraderTools/CopyEngine.cs --CCN 8` | IsPendingBeSlotActive: 0 warnings (CYC=1 if added) |
| SCAN-06 ASCII | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/Tests/BwaveDwLaneATests.cs` | 0 results |
| SCAN-07 xUnit | `grep -n "\[Fact\]" src/PropTraderTools/Tests/BwaveDwLaneATests.cs` | all [Fact], never [Test] |

---

## Ticket 4 -- DW-NEW-08 Option E: Accelerated Naked Detection

**File(s)**: `src/PropTraderTools/CopyEngine.cs`
**Type**: Production Fix
**Spec Req IDs**: DW-NEW-08 (Option E -- Layer 1 only; Option D is Lane B)
**Dependencies**: None -- can start immediately, parallel with T5

### Change Description

Hook accelerated naked position detection into `OnOrderUpdate` as an unconditional pre-Gate-1
tail-call. Target: shrink naked position window from ~2000ms (REAPER timer) to ~50ms
(order-update event dispatch).

**Add 1 new field** near other CopyEngine debounce fields:
```csharp
// DW-NEW-08 Option E: debounce dict for naked detection.
// Stores Environment.TickCount64 at last naked-detect queue time per account name.
// ConcurrentDictionary: no lock. Key = acc.Name (NT8 platform account name).
private readonly ConcurrentDictionary<string, long> _nakedDetectLastQueuedTicks =
    new ConcurrentDictionary<string, long>();
```

**Add tail-call to `OnOrderUpdate`** in the pre-Gate-1 block (after any existing pre-Gate-1 helpers,
before Gate 1 check -- engineer confirms exact insertion point):
```csharp
// DW-NEW-08 Option E: detect naked position within 50ms of terminal order event.
TryNakedDetect(e);
```

**Add 4 new private methods** (at end of CopyEngine.cs or grouped with REAPER methods):

```csharp
// DW-NEW-08 Option E: thin dispatcher gate.
// CYC=3: (1) terminal-state check, (2) follower-account check, (3) NakedPositionDetector call.
// JS-021: no lock. JS-001: no throw. JS-033: synchronous void.
private void TryNakedDetect(OrderEventArgs e)
{
    if (
        e.Order.OrderState != OrderState.Filled
        && e.Order.OrderState != OrderState.Cancelled
        && e.Order.OrderState != OrderState.Rejected
    )
        return;
    if (!IsFollowerAccount(e.Order.Account))
        return;
    NakedPositionDetector(e.Order.Account);
}

// DW-NEW-08 Option E: check and queue flatten if follower account is naked.
// CYC<=6: (1) acct null, (2) HasNakedPosition, (3) debounce check,
//         (4) AddOrUpdate atomic, (5) instrument null check.
// JS-021: no lock -- ConcurrentDictionary atomic ops only.
// JS-001: no throw. JS-033: synchronous void.
// NT8 bans: no Account.Change(), no AtmStrategyCreate(), no AtmStrategyChangeStopTarget().
private void NakedPositionDetector(Account acct)
{
    if (acct == null)
        return;
    if (!HasNakedPosition(acct))
        return;

    // debounce: skip if already queued within 500ms grace window
    long now = Environment.TickCount64;
    const long GraceMs = 500L;
    long last = _nakedDetectLastQueuedTicks.GetOrAdd(acct.Name, 0L);
    if (now - last < GraceMs)
        return;

    // atomic update: only proceed if our 'now' beat any concurrent thread
    long prev = _nakedDetectLastQueuedTicks.AddOrUpdate(
        acct.Name, now, (_, __) => now);
    if (prev != now)
        return;

    // marshal flatten to UI thread -- same pattern as other flatten paths
    Instrument? instr = FindOpenPositionInstrument(acct);
    if (instr is not null)
        NinjaTrader.Core.Globals.Dispatcher.InvokeAsync(() =>
            FlattenOneAccount(acct, instr));
}

// DW-NEW-08 Option E: naked position check.
// Returns true if acc has a non-flat position AND zero Working/PendingSubmit Stop or Target.
// CYC<=8 (see analysis below).
// JS-021: no lock. JS-002: returns bool. JS-001: no throw.
private static bool HasNakedPosition(Account acct)
{
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
        return false;

    bool hasStop = false;
    bool hasTarget = false;
    foreach (Order o in acct.Orders)
    {
        if (
            o.OrderState != OrderState.Working
            && o.OrderState != OrderState.PendingSubmit
        )
            continue;
        if (o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit)
            hasStop = true;
        else if (o.OrderType == OrderType.Limit)
            hasTarget = true;
    }
    return !hasStop && !hasTarget;
}

// DW-NEW-08 Option E: return instrument of first non-flat position, or null if all flat.
// CYC=1. JS-002 compliant: return type Instrument? (nullable). No raw `return null`.
// Caller uses `is not null` guard (see NakedPositionDetector above).
private static Instrument? FindOpenPositionInstrument(Account acct) =>
    acct.Positions.FirstOrDefault(static p => p.Quantity > 0)?.Instrument;
```

### Method Signatures (new/modified)

```csharp
// NEW:
private void TryNakedDetect(OrderEventArgs e)
private void NakedPositionDetector(Account acct)
private static bool HasNakedPosition(Account acct)
private static Instrument? FindOpenPositionInstrument(Account acct)

// FIELD:
private readonly ConcurrentDictionary<string, long> _nakedDetectLastQueuedTicks

// MODIFIED (tail-call added, no signature change):
// private void OnOrderUpdate(object sender, OrderEventArgs e)
// Change: add TryNakedDetect(e); call in pre-Gate-1 block
```

### CYC Analysis

| Method | CYC | Limit | Lizard Expected |
|--------|-----|-------|----------------|
| `TryNakedDetect` | 3 | 8 | 0 warnings |
| `NakedPositionDetector` | 5-6 | 8 | 0 warnings |
| `HasNakedPosition` | <=8 | 8 | 0 warnings |
| `FindOpenPositionInstrument` | 1 | 8 | 0 warnings |
| `OnOrderUpdate` (modified) | Unchanged | 8 | 0 warnings (TryNakedDetect is unconditional -- adds 0 to parent CYC) |

**HasNakedPosition CYC note**: Lizard will count 7-8 branches (2 foreach loops + 5 decision points).
Plan's conceptual "CYC=4" does not match Lizard's mechanical count. The method is within the <=8
budget regardless. Run `lizard HasNakedPosition --CCN 8` to confirm 0 warnings after implementation.

**Risk note** (from plan §6): Calibrate the 500ms grace window against a SIM gate. If `[NAKED-DETECT]`
log lines appear during normal fill+bracket-arm sequences, increase `GraceMs`. If naked positions
are missed, decrease it. Document calibration result in `ticket-4-completion.md`.

### Acceptance Criteria

1. `TryNakedDetect(e)` tail-call added in `OnOrderUpdate` pre-Gate-1 block
2. `OnOrderUpdate` CYC unchanged (unconditional call adds 0 branches)
3. `NakedPositionDetector` fires within ~50ms of a Filled/Cancelled/Rejected event on a naked follower account
4. No false fires during normal ATM bracket confirmation lag (500ms grace window in `_nakedDetectLastQueuedTicks`)
5. Multi-follower isolation: PA-04 naked does NOT queue a flatten for PA-03
6. No `lock()`, no `Account.Change()`, no `AtmStrategyCreate()`, no `AtmStrategyChangeStopTarget()`
7. All new methods CYC <=8, `lizard --CCN 8`: 0 warnings
8. `Dispatcher.InvokeAsync` used for `FlattenOneAccount` marshal -- NOT called directly from order callback
9. `FindOpenPositionInstrument` returns `Instrument?` -- no raw `return null` statement
10. `dotnet build` 0 errors, 0 warnings
11. SIM gate: fill entry on follower with no brackets present → `[NAKED-DETECT]` log fires within 50ms
12. All 4 recommended [Fact] tests pass

### Test Coverage

All T4 tests go in `BwaveDwLaneATests.cs` (append) or `BwaveNextLaneATests.cs`.
Use `HasNakedPosition` and `NakedPositionDetector` via `[InternalsVisibleTo]` or reflection.
Engineer adds `[assembly: InternalsVisibleTo("PropTraderTools.Tests")]` to CopyEngine.cs assembly
if not already present.

```csharp
[Fact]
public void HasNakedPosition_ReturnsFalse_WhenNoPosition()
{
    // Arrange: account with Positions collection empty or all Quantity==0.
    // Assert: HasNakedPosition(acct) == false
}

[Fact]
public void HasNakedPosition_ReturnsFalse_WhenStopOrderPresent()
{
    // Arrange: account with one non-flat position (Quantity > 0)
    //          AND one Working StopMarket order.
    // Assert: HasNakedPosition(acct) == false
    //         (has position, but has stop -- NOT naked)
}

[Fact]
public void HasNakedPosition_ReturnsTrue_WhenNoProtectiveOrders()
{
    // Arrange: account with one non-flat position (Quantity > 0)
    //          AND zero Working/PendingSubmit Stop or Target orders.
    // Assert: HasNakedPosition(acct) == true
    //         (has position, no stop, no target -- IS naked)
}

[Fact]
public void NakedPositionDetector_DoesNotFire_WithinGraceWindow()
{
    // Arrange:
    //   Set _nakedDetectLastQueuedTicks[acct.Name] = Environment.TickCount64 via reflection
    //   (simulates a recent dispatch within 500ms).
    //   Account is naked (HasNakedPosition returns true).
    // Act: NakedPositionDetector(acct) called immediately
    // Assert: FlattenOneAccount NOT called (debounce suppressed it)
    //         (verify via Moq mock on FlattenOneAccount OR by checking _nakedDetectLastQueuedTicks
    //          tick value has NOT been updated by a second call)
}
```

### NT8 Sync Requirement

**REQUIRED.** Run after all T4 changes are complete:
```powershell
powershell -File scripts\ptt-sync-and-verify.ps1
```
Expected: `18/18 OK, 0 MISMATCH`.
Record output verbatim in `ticket-4-completion.md`.
Then press **F5** in NinjaTrader 8. Confirm 0 new errors.

### 7-Scan Checklist

| Scan | Command | Expected Result |
|------|---------|----------------|
| SCAN-01 JS-021 lock() | `grep -r "lock\s*(" src/PropTraderTools --include="*.cs"` | 0 results in new/modified code |
| SCAN-02 JS-033 async void | `grep -r "async void [A-Z]" src/PropTraderTools --include="*.cs"` | 0 results in new/modified code |
| SCAN-03 JS-002 return null | `grep -n "return null" src/PropTraderTools/CopyEngine.cs` | 0 new `return null` statements (FindOpenPositionInstrument uses `?.Instrument`, not `return null`) |
| SCAN-04 JS-001 throw | `grep -n "throw new" src/PropTraderTools/CopyEngine.cs` | 0 new exceptions in T4 methods |
| SCAN-05 CYC<=8 | `lizard src/PropTraderTools/CopyEngine.cs --CCN 8` | TryNakedDetect=3, NakedPositionDetector<=6, HasNakedPosition<=8, FindOpenPositionInstrument=1: 0 warnings |
| SCAN-06 ASCII | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` | 0 results |
| SCAN-07 xUnit | `grep -n "\[Fact\]" src/PropTraderTools/Tests/BwaveDwLaneATests.cs` | all [Fact], never [Test] |

---

## Ticket 5 -- DW-NEW-09: ActiveOrders Filter Wrapper

**File(s)**: `src/PropTraderTools/CopyEngine.cs`
**Type**: Production Fix (small -- 1 new method + 2 one-line call-site changes)
**Spec Req IDs**: DW-NEW-09
**Dependencies**: None -- can start immediately, parallel with T4

### Change Description

Add one static private helper and change exactly 2 call sites. All other 23 `acc.Orders.ToList()`
call sites are explicitly **UNCHANGED**.

**Add new method** `ActiveOrders` (place near other private static helpers in CopyEngine.cs):

```csharp
// DW-NEW-09: ActiveOrders -- terminal-state filter for Account.Orders.
// Returns only orders in non-terminal states (Filled/Cancelled/Rejected excluded).
// CYC=1: expression body, single Where predicate, no branching.
// JS-021: no lock (LINQ Where is non-mutating). JS-002: IEnumerable<Order> (never null).
// JS-036: lazy Where -- no heap allocation beyond the enumerator.
// Fix point: callers that need active orders use this instead of .ToList().
// NT8: acc.Orders iteration is safe on order-update callback thread (same as existing ToList() pattern).
private static IEnumerable<Order> ActiveOrders(Account acc) =>
    acc.Orders.Where(static o =>
        o.OrderState != OrderState.Filled
        && o.OrderState != OrderState.Cancelled
        && o.OrderState != OrderState.Rejected);
```

**Change 1 -- Line 3437** (`FindFollowerBracketOrder` Account overload -- calls the IEnumerable overload):

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

Note: The `FindFollowerBracketOrder(IEnumerable<Order>, ...)` overload at line 3452 already accepts
`IEnumerable<Order>`. No signature change needed.

**Change 2 -- Line 3637** (`FindFollowerEntryOrder`):

```csharp
// BEFORE:
foreach (var order in follower.Orders.ToList()) // (1)

// AFTER:
foreach (var order in ActiveOrders(follower)) // (1) DW-NEW-09: terminal orders excluded
```

**Explicitly UNCHANGED call sites** (engineer must verify these are untouched):

| Line | Method | Reason UNCHANGED |
|------|--------|-----------------|
| ~1708 | `CancelPttDragOrphansForAccount` | Has `IsPttDragOrphanCancellable` gate -- scans for specific state |
| ~1947 | `TryLogSFBTrace` | Diagnostic -- intentionally shows full order history |
| All other ~21 sites | Various | Each has own state gate or scans for different purpose |

**Engineer verification**: After changes, confirm only 2 `follower.Orders.ToList()` references were
replaced. Run:
```powershell
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "\.Orders\.ToList\(\)" | Measure-Object | Select-Object -ExpandProperty Count
```
Expected count: 23 (was 25 before T5; 2 replaced with ActiveOrders).

### Method Signatures (new/modified)

```csharp
// NEW:
private static IEnumerable<Order> ActiveOrders(Account acc)

// MODIFIED call sites (signatures unchanged, argument changed):
// Line 3437: FindFollowerBracketOrder Account overload -- arg changed
// Line 3637: FindFollowerEntryOrder -- foreach source changed
```

### CYC Analysis

| Method | Before | After | Lizard Expected |
|--------|--------|-------|----------------|
| `ActiveOrders` | N/A | 1 | 0 warnings (new, CYC=1) |
| `FindFollowerBracketOrder` Account overload (line 3430) | 1 | 1 | 0 warnings (unchanged) |
| `FindFollowerEntryOrder` (line 3635) | 3 | 3 | 0 warnings (unchanged) |

### Acceptance Criteria

1. `ActiveOrders(Account)` helper added: CYC=1, `static`, `private`, no lock, lazy `Where` (no `ToList()`)
2. `FindFollowerBracketOrder` Account overload (line 3437): uses `ActiveOrders(follower)` instead of `follower.Orders.ToList()`
3. `FindFollowerEntryOrder` (line 3637): uses `ActiveOrders(follower)` instead of `follower.Orders.ToList()`
4. ALL 23 other `acc.Orders.ToList()` call sites: **unchanged**
5. `TryLogSFBTrace` diagnostic (line ~1947): **unchanged** (intentional full history dump)
6. `FindFollowerBracketOrderTestable(IEnumerable<Order>, ...)` test seam (lines 3583/3593): **unchanged**
7. `.Orders.ToList()` count after T5: exactly 23 (was 25; 2 replaced)
8. `dotnet build` 0 errors, 0 warnings
9. `[Fact] FindFollowerBracketOrder_SkipsFilledAndCancelledOrders()` passes
10. `[Fact] FindFollowerEntryOrder_SkipsFilledAndCancelledEntries()` passes
11. No `lock()`, CYC <=8 all modified methods, ASCII-only, xUnit-only

### Test Coverage

```csharp
[Fact]
public void FindFollowerBracketOrder_SkipsFilledAndCancelledOrders()
{
    // Arrange:
    //   Build an IEnumerable<Order> with:
    //     14 orders in OrderState.Cancelled (named "Stop1", OrderType.StopMarket)
    //     1 order in OrderState.Working (named "Stop1", OrderType.StopMarket)
    //   Pass to FindFollowerBracketOrderTestable(orders, "Stop1", isStop: true, leaderName: "leader")
    //   (test seam at line 3583 accepts IEnumerable<Order>)
    //
    // Assert:
    //   returned order is not null
    //   returned order.OrderState == OrderState.Working
    //   returned order.Name == "Stop1"
    //   (confirms cancelled orders skipped, Working stop found)
    //
    // Alternative approach if FindFollowerBracketOrderTestable is not accessible:
    //   Test ActiveOrders helper directly:
    //   var result = ActiveOrders(mockAccount).ToList(); // mockAccount has 14 Cancelled + 1 Working
    //   Assert.Equal(1, result.Count);
    //   Assert.Equal(OrderState.Working, result[0].OrderState);
}

[Fact]
public void FindFollowerEntryOrder_SkipsFilledAndCancelledEntries()
{
    // Arrange:
    //   Build input with:
    //     1 order: Name="PTT-Copy", OrderState=Cancelled, OrderType=Limit
    //     1 order: Name="PTT-Copy", OrderState=Working, OrderType=Limit
    //   If FindFollowerEntryOrder has no test seam:
    //     Test ActiveOrders directly -- filter should return only the Working order.
    //   If seam available: exercise via test seam directly.
    //
    // Assert:
    //   returned order.OrderState == OrderState.Working
    //   returned order.Name == "PTT-Copy"
    //   (confirms Cancelled entry skipped, Working entry returned)
}
```

**Engineer note**: `FindFollowerBracketOrderTestable(IEnumerable<Order>, ...)` at lines 3583/3593
provides a test seam for bracket order tests. For `FindFollowerEntryOrder`, if no test seam exists,
test `ActiveOrders` helper directly with a mocked `Account` that returns the prepared order list.
The `static` helper is easiest to test by passing a mocked/stub `Account`.

### NT8 Sync Requirement

**REQUIRED.** Run after all T5 changes are complete:
```powershell
powershell -File scripts\ptt-sync-and-verify.ps1
```
Expected: `18/18 OK, 0 MISMATCH`.
Record output verbatim in `ticket-5-completion.md`.
Then press **F5** in NinjaTrader 8. Confirm 0 new errors.

### 7-Scan Checklist

| Scan | Command | Expected Result |
|------|---------|----------------|
| SCAN-01 JS-021 lock() | `grep -r "lock\s*(" src/PropTraderTools --include="*.cs"` | 0 results in new/modified code |
| SCAN-02 JS-033 async void | `grep -r "async void [A-Z]" src/PropTraderTools --include="*.cs"` | 0 results in new/modified code |
| SCAN-03 JS-002 return null | `grep -n "return null" src/PropTraderTools/CopyEngine.cs` | 0 new occurrences (ActiveOrders returns IEnumerable, never null) |
| SCAN-04 JS-001 throw | `grep -n "throw new" src/PropTraderTools/CopyEngine.cs` | 0 new exceptions in T5 methods |
| SCAN-05 CYC<=8 | `lizard src/PropTraderTools/CopyEngine.cs --CCN 8` | ActiveOrders=1, FindFollowerBracketOrder=1, FindFollowerEntryOrder=3: 0 warnings |
| SCAN-06 ASCII | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` | 0 results |
| SCAN-07 xUnit | `grep -n "\[Fact\]" src/PropTraderTools/Tests/BwaveDwLaneATests.cs` | all [Fact], never [Test] |

---

## Post-Implementation Gates Summary

| Ticket | Production Change | NT8 Sync Required | F5 Required |
|--------|------------------|-------------------|-------------|
| T1 | ZERO (test only) | NO | NO |
| T2 | YES (TradeCopierPanel.cs) | YES -- `18/18 OK, 0 MISMATCH` | YES |
| T3 | NO (or 1-line seam in CopyEngine.cs) | CONDITIONAL (if seam added) | CONDITIONAL |
| T4 | YES (CopyEngine.cs) | YES -- `18/18 OK, 0 MISMATCH` | YES |
| T5 | YES (CopyEngine.cs) | YES -- `18/18 OK, 0 MISMATCH` | YES |

**Sessions A (T1+T2) and B (T4+T5) can run concurrently.** Different source files, no conflict.
**Session C (T3) starts only after T1 VERIFY_PASS is confirmed.**

---

*Tickets written: 2026-09-04 | ptt-architect | BWAVE-NEXT Lane A*
*Source plan: 02-architecture-plan.md (REVIEW_PASS cycle 2)*
*8 sequential thoughts completed before writing*
