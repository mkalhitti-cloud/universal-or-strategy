# BWAVE-NEXT Lane A -- Mission Brief

**Lane**: A -- Teardown Lifecycle + Integration Test
**Status**: SPEC_READY -- awaiting wave launch
**Source decisions**: Director session 2026-09-04
**Brain dir**: `docs/brain/BWAVE-NEXT/LaneA/`

---

## Director Decisions Recorded

| Decision | Choice | Date |
|----------|--------|------|
| DW-LaneA-06: BuildArrowCluster background overwrite | **Option B -- collapse inline into BuildBufferedButtonsRow** | 2026-09-04 |
| DW-C38-01: Unsubscribe pattern for OnPendingBeArmedDispatch | **Unconditional unsubscribe, no tracking flag needed** | 2026-09-04 |
| DW-DW-03: Test realism for sibling-isolation coverage | **Realistic integration test required (two-panel scenario)** | 2026-09-04 |

---

## Pre-Work Finding (IMPORTANT -- Read Before Architecting)

**DW-C38-01 is already resolved.** Source inspection of `TradeCopierPanel.cs` line 586
confirms `_engine.PendingBeArmed -= OnPendingBeArmedDispatch` is already present in
`Detach()`. This was added as `HOTFIX-BEALL-SYNC-01`. Do NOT re-add it.

Full unsubscribe sequence already in `Detach()` (lines 583-589):
```
_engine.StatusUpdate            -= OnStatusUpdate
_engine.PositionStateChanged    -= OnPositionStateChanged
_engine.PendingBeFired          -= OnPendingBeFiredDispatch
_engine.PendingBeArmed          -= OnPendingBeArmedDispatch        // ALREADY PRESENT
_engine.GlobalBeBufferChanged   -= OnGlobalBeBufferChanged
_engine.GlobalQuickAllBufferChanged -= OnQuickAllBufferChanged
_engine.GlobalBeAllDisarmed     -= OnGlobalBeAllDisarmed
```

This changes the scope of this lane from 4 items to 3 items.

---

## Scope -- 3 Tickets

| Ticket | DW Item | File | Type | Director Decision |
|--------|---------|------|------|-------------------|
| **T1** | DW-C38-04 | `TradeCopierPanel.cs` | Production fix | Unsubscribe follower `OrderUpdate`/`PositionUpdate` handlers before `_allAccounts.Clear()` in `Detach()` |
| **T2** | DW-LaneA-06 (CYC) | `TradeCopierPanel.cs` | Production fix | Collapse `BuildArrowCluster` inline into `BuildBufferedButtonsRow` (Option B) |
| **T3** | DW-DW-03 + DW-NEW-07 AC | `BwaveDwLaneATests.cs` or new test file | Test only | Two-panel integration test: arm BE both panels, close panel 1, verify panel 2 unaffected; close panel 2, verify all slots cleared |
| **T4** | DW-NEW-08 (Option E) | `CopyEngine.cs` or new partial file | Production fix (small) | Accelerated naked detection: hook NakedPositionDetector into OnAccountOrderUpdate; shrink naked window from ~2000ms to ~50ms |
| **T5** | DW-NEW-09 | `CopyEngine.cs` | Production fix (small) | `ActiveOrders` filter wrapper: replace `follower.Orders.ToList()` in `FindFollowerBracketOrder` + `FindFollowerEntryOrder` with terminal-state-filtered view; 2 tests |

Tickets are **independent**. T1 and T2 touch production code (`TradeCopierPanel.cs` only,
different regions). T3 touches test code only. T1 and T2 may be executed in parallel.
T3 requires T1 to be merged first (its test exercises the Detach path T1 modifies).
T4 and T5 are both `CopyEngine.cs` changes but touch different regions and are independent.

---

## Ticket T1 -- DW-C38-04: Unsubscribe follower handlers before _allAccounts.Clear()

### Problem (plain English)

When a panel closes, `_allAccounts.Clear()` at line 620 discards the list of all accounts
the panel was managing. But before that list is cleared, the follower accounts (not the
leader) had `OrderUpdate` and `PositionUpdate` subscriptions wired in `PopulateFollowerItems`
(line 743) and the setup path (lines 838-839 for the leader).

The leader account's `OrderUpdate`/`PositionUpdate` handlers are correctly unsubscribed at
lines 601-602 (inside `if (_leaderAccount != null)` guard, set to null at line 610).

The follower accounts' `AccountItemUpdate` handler is correctly unsubscribed by
`UnsubscribeFollowerItems()` at line 590 (iterates `_followerItems`).

**The gap**: `_followerItems` stores `FollowerItem` objects. `_allAccounts` stores raw
`Account` references. `_allAccounts` is populated by aggregating leader + follower accounts
into a flat list (lines 815-820). It is passed to `IPttModule.Initialize(this)` so modules
can iterate all accounts. When `_allAccounts.Clear()` fires without prior handler removal,
any module that subscribed to an account event via `_allAccounts` enumeration retains a
live reference.

**Concrete risk**: A module holding an `Account` from `_allAccounts` may still receive
`OrderUpdate` or `PositionUpdate` callbacks after the panel that owns those accounts
has been torn down, because the Account object itself is still live in NT8 memory.

### Source Reference

```
Line 590:  UnsubscribeFollowerItems();          // correctly unsubscribes AccountItemUpdate
Line 598-603: leader OrderUpdate/PositionUpdate unsubscribed (correct)
Line 616-619: _modules.Teardown() + _modules.Clear()
Line 620:  _allAccounts.Clear();                // GAP: no prior per-account handler removal
```

### Fix

`_allAccounts` itself does not hold direct event subscriptions -- the subscriptions are
held by the `IPttModule` instances. The correct fix is to ensure `_modules.Teardown()` is
called **before** `_allAccounts.Clear()`, and to verify that each module's `Teardown()`
method correctly unsubscribes from any account-level events it subscribed to during
`Initialize()`.

**Step 1**: Confirm ordering is already correct -- `_modules.Teardown()` at line 617
already precedes `_allAccounts.Clear()` at line 620. If so, T1 becomes a verification
ticket with a targeted unit test rather than a code change.

**Step 2**: Grep each `IPttModule` implementation for `OrderUpdate +=` and
`PositionUpdate +=` usage, confirm each has a matching `-=` in its `Teardown()`.

**Step 3**: If any module is missing the unsubscribe in `Teardown()`, add it there
(not in `TradeCopierPanel.Detach()`). Each module owns its own subscriptions.

### Acceptance Criteria

- [ ] All `IPttModule` implementations that subscribe to `OrderUpdate`/`PositionUpdate`
      in `Initialize()` have a matching unsubscribe in `Teardown()`
- [ ] Ordering in `Detach()` confirmed: `_modules.Teardown()` before `_allAccounts.Clear()`
- [ ] No new `lock()` introduced
- [ ] CYC of any modified method remains ≤ 8
- [ ] 1 xUnit `[Fact]`: verifies that after `Detach()`, no module holds a live account reference

### Test Name
```
[Fact] Detach_ClearsAllModulesBeforeAccountList()
```

---

## Ticket T2 -- DW-LaneA-06: Collapse BuildArrowCluster inline (Option B)

### Problem (plain English)

`BuildArrowCluster` is a helper method that builds the button+arrows widget used for
Trim, Flatten, BE, BE ALL, Quick, Quick ALL. It receives a `mainBackground` brush
parameter and sets `btn.Background = mainBackground` at line 1225.

The teal buttons (BE, BE ALL, Quick, Quick ALL -- those with `useTealBorder = true`)
pass `BrushInactive` as the background at construction time (lines 1157-1160 in the specs
array). But after `BuildArrowCluster` sets the background, it then calls
`btn.SetResourceReference(Control.StyleProperty, "NTButtonStyle")` at line 1232.

`NTButtonStyle` is a NinjaTrader WPF resource that may override `btn.Background` via
a style setter, which would stomp the explicitly set brush. The teal border/foreground
(set at lines 1228-1230) survive because they are set on named properties that the style
does not override. The background is at risk.

**Option B** means: remove `BuildArrowCluster` as a separate method and write the
button construction directly inside `BuildBufferedButtonsRow`, where the specs array
already drives the per-button parameterization. This eliminates the parameter-passing
indirection and makes the construction order explicit and auditable.

### Source Reference

```
Line 1131: private void BuildBufferedButtonsRow(StackPanel root)
Line 1144-1167: specs array + foreach loop calling BuildArrowCluster
Line 1188-1237: BuildArrowCluster (to be inlined)
Line 1164: var (cluster, btn) = BuildArrowCluster(s.Content, s.Bg, s.Teal, s.Up, s.Dn, s.Main);
```

### Fix

Inline the body of `BuildArrowCluster` into the `foreach` loop inside
`BuildBufferedButtonsRow`, replacing the call at line 1164. The method `BuildArrowCluster`
is called from **exactly one place** (line 1164 -- confirmed by grep). After inlining,
delete the method definition entirely (lines 1188-1237).

Preserve the `useTealBorder` conditional block (lines 1226-1231). Preserve all
`SetResourceReference` calls. Do not change the `specs` array layout.

After inlining, the background property should be set **after** `SetResourceReference`
so that the explicit brush wins over any style default:

```csharp
btn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
btn.Background = s.Bg;   // set AFTER style -- explicit brush wins
```

### CYC Impact

`BuildArrowCluster` CCN=2 is deleted. `BuildBufferedButtonsRow` absorbs its logic.
`BuildBufferedButtonsRow` CCN before inline: verify with lizard. After inline: the
foreach body grows by ~10 lines but adds 1 branch (`if (s.Teal)`). Target CCN ≤ 8.

### Acceptance Criteria

- [ ] `BuildArrowCluster` method deleted entirely
- [ ] `BuildBufferedButtonsRow` inlines the cluster construction; all 6 button specs work
- [ ] Teal buttons (BE, BE ALL, Quick, Quick ALL) retain teal border+foreground
- [ ] Background brush set **after** `SetResourceReference` in inlined code
- [ ] `dotnet build` 0 errors, 0 warnings
- [ ] `lizard BuildBufferedButtonsRow --CCN 8`: ≤ 8
- [ ] No `lock()`, no `async void`, no `return null`, ASCII-only

### Test Names
```
[Fact] BuildBufferedButtonsRow_TealButtons_HaveTealBorderBrush()
[Fact] BuildBufferedButtonsRow_TrimButton_HasInactiveBackground()
```
*(Reflection-based: instantiate panel stub, invoke BuildBufferedButtonsRow via reflection,
inspect button properties from the specs store fields `_beBtn2`, `_trimBtn2`, etc.)*

---

## Ticket T3 -- DW-DW-03 + DW-NEW-07: Two-Panel Integration Test

### Problem (plain English)

The current tests for the sibling-isolation fix (DW-C38-03) only check that the
`DisarmAllAccounts` method no longer exists in code. They do not simulate a real scenario
where two panels are running, both have break-even armed, and one panel closes.

This test fills that gap AND validates the DW-NEW-07 fix (R-LC-2): that when the
**last** panel closes, all BE slots are fully cleared.

### Three scenarios to cover

| Scenario | Setup | Action | Assert |
|----------|-------|--------|--------|
| **S1: Sibling isolation** | Two panels, BE armed on both | Close panel A | Panel B's BE slot still armed (`IsPendingSlotArmed(panelB) == true`) |
| **S2: Own-account cleanup** | One panel, BE armed | Close that panel | Its own slot cleared (`IsPendingSlotArmed(panelA) == false`) |
| **S3: Last-panel global cleanup** | Two panels, BE armed on both | Close panel A, then close panel B | After panel B close: all pending slots empty (`IsPendingSlotsEmpty() == true`) |

### Test Approach

Use the same reflection + CopyEngine instance pattern established in `BwaveDwLaneATests.cs`.
`CopyEngine.Instance` is a singleton that can be seeded with pending BE slots via
`ArmPendingBe(account)` (or equivalent). `Detach()` can be called by exercising the
`TradeCopierAddOn` panel registry (or mocked by calling `IsPanelsEmpty` with a
controlled `_panels` state).

If full `TradeCopierPanel` construction is too heavyweight (WPF STA requirement),
the test should exercise `CopyEngine.DisarmPendingBe` and
`CopyEngine.ClearAllPendingBeSlots` directly -- these are the actual units the fix
touches -- and separately assert the `IsPanelsEmpty` guard logic.

**Engineer must investigate**: Can `TradeCopierAddOn._panels` be seeded in a test without
instantiating the full WPF panel? If yes, use real `Detach()`. If no, drive the
`CopyEngine` methods directly and test the guard logic via the public/internal API.

### Test Names
```
[Fact] Detach_PanelA_DoesNotClearPanelB_BeSlot()
[Fact] Detach_LastPanel_ClearsAllPendingBeSlots()
[Fact] Detach_OwnPanel_ClearsOwnBeSlot_ButNotOthers()
```

### Acceptance Criteria

- [ ] All 3 `[Fact]` methods pass without `[Skip]`
- [ ] No WpfFact / STA workaround needed (if CopyEngine is exercised directly)
- [ ] No `lock()`, xUnit-only, ASCII-only
- [ ] Test file: `src/PropTraderTools/Tests/BwaveDwLaneATests.cs` (append to existing file)
      OR new `BwaveNextLaneATests.cs` -- engineer chooses based on file size
- [ ] Depends on T1 being merged first (tests the code T1 verified)

---

## Ticket T5 -- DW-NEW-09: ActiveOrders filter for FindFollowerBracketOrder + FindFollowerEntryOrder

### Problem (plain English)

Every `acc.Orders.ToList()` scan in CopyEngine iterates the **full session history**
of the account including terminal orders (Filled, Cancelled, Rejected). After a
drag-repositioning session with 14+ cancel/resubmit cycles, this list is dominated by
stale terminal entries that have no actionable value to any scanner.

The two primary bracket/entry finders (`FindFollowerBracketOrder` and
`FindFollowerEntryOrder`) have internal state filters that correctly skip terminal
orders. The code produces correct results today. The risk is structural fragility:
correctness depends on every future code change maintaining comprehensive state filters
at every call site.

The Jane Street principle: make the illegal input disappear before it reaches logic.
A single filter wrapper at entry eliminates the fragility across all current and
future callers.

### Source Reference

```
Line 3437: follower.Orders.ToList()  -- passed into FindFollowerBracketOrder
Line 3637: follower.Orders.ToList()  -- iterated directly in FindFollowerEntryOrder
Line 1958: acc.Orders.ToList()       -- TryLogSFBTrace diagnostic dump (intentional, leave)
```

### Fix

Add one static helper:

```csharp
// ActiveOrders: CYC=1. Terminal-state filter -- Filled/Cancelled/Rejected excluded.
// JS-021: no lock. JS-002: IEnumerable<Order> (no null). JS-036: lazy Where, no alloc.
private static IEnumerable<Order> ActiveOrders(Account acc) =>
    acc.Orders.Where(static o =>
        o.OrderState != OrderState.Filled
        && o.OrderState != OrderState.Cancelled
        && o.OrderState != OrderState.Rejected);
```

Change two call sites only:

```csharp
// Line 3437 (FindFollowerBracketOrder Account overload):
FindFollowerBracketOrder(ActiveOrders(follower), ...)  // was: follower.Orders.ToList()

// Line 3637 (FindFollowerEntryOrder):
foreach (var order in ActiveOrders(follower))  // was: follower.Orders.ToList()
```

All 23 other `acc.Orders.ToList()` call sites are unchanged.

### CYC Impact

All modified methods remain CYC <= 8. `ActiveOrders` CYC = 1.

### Acceptance Criteria

- [ ] `ActiveOrders(Account)` helper added: CYC=1, `static`, `private`, no lock, no alloc
- [ ] `FindFollowerBracketOrder` Account overload (line 3437): uses `ActiveOrders(follower)`
- [ ] `FindFollowerEntryOrder` (line 3637): uses `ActiveOrders(follower)`
- [ ] All 23 other `acc.Orders.ToList()` call sites: **unchanged**
- [ ] `TryLogSFBTrace` diagnostic (line 1947): **unchanged** (intentional full history dump)
- [ ] `dotnet build` 0 errors, 0 warnings
- [ ] `[Fact] FindFollowerBracketOrder_SkipsFilledAndCancelledOrders()`:
      inject 14 Cancelled + 1 Working StopMarket order; assert Working stop returned
- [ ] `[Fact] FindFollowerEntryOrder_SkipsFilledAndCancelledEntries()`:
      inject 1 Cancelled "PTT-Copy" + 1 Working "PTT-Copy" Limit; assert Working returned
- [ ] No lock(), CYC <= 8, ASCII-only, xUnit-only

### Test Names
```
[Fact] FindFollowerBracketOrder_SkipsFilledAndCancelledOrders()
[Fact] FindFollowerEntryOrder_SkipsFilledAndCancelledEntries()
```

---

## Out of Scope (explicitly excluded from this lane)

The following items were identified in the backlog but are **not** in this lane's scope.
Do not act on these during this lane:

| ID | Reason excluded |
|----|----------------|
| DW-C38-02 (`_modules.Teardown()` Dispose verification) | Analysis only, no crash observed; requires module-by-module audit, separate ticket |
| DW-C38-01 (`OnPendingBeArmedDispatch` unsubscribe) | **Already resolved** -- line 586 in current source. Not a gap. |
| DW-C39-09 (`SaveRules` on `OnAddRule`) | `TradeCopierWindow.cs` scope -- different file, separate lane |
| DW-C39-07/08 (null-guards, rule-count cap) | `TradeCopierWindow.cs` scope -- separate lane |
| DW-RepairLC-01/02 (SIM gates) | Requires live NT8 session -- Director action, not engineer ticket |
| DW-NEW-07 live-trading observations | Director will provide; separate backlog append, not a code ticket |

---

## Files in Scope

| File | Tickets | Change Type |
|------|---------|-------------|
| `src/PropTraderTools/TradeCopierPanel.cs` | T1, T2 | Production -- requires NT8 sync + F5 |
| `src/PropTraderTools/Tests/BwaveDwLaneATests.cs` OR new test file | T3 | Test only -- no NT8 sync |
| `PropTraderTools.csproj` | T3 (if new file) | Add `<Compile Include>` entry |

---

## Post-Implementation Gates (all tickets)

```powershell
# T1 + T2 (production changes):
powershell -File scripts\ptt-sync-and-verify.ps1  # must show 18/18 OK, 0 MISMATCH
# Then: F5 in NinjaTrader 8 -- 0 new errors

# T3 (test only -- no sync required):
dotnet build   # 0 errors
dotnet test    # 0 new failures
```

---

## Jane Street Compliance Checklist (all tickets)

| Rule | Requirement |
|------|------------|
| JS-021 | No `lock()` anywhere in new or modified code |
| JS-033 | No `async void` (non-event-handler) |
| JS-002 | No `return null` in new code |
| JS-001 | No `throw new XxxException` in hot paths |
| CYC ≤ 8 | All new and modified methods ≤ 8 |
| ASCII-only | No Unicode, emoji, curly quotes in string literals |
| xUnit-only | `[Fact]`, `Assert.*` -- no NUnit, no MSTest |

---

## Execution Order

```
T1 (verify module teardown) ──┐
                               ├──> BOTH pass ──> T3 (integration test)
T2 (inline BuildArrowCluster) ─┘
```

T1 and T2 are independent and may run in the same engineer session or in parallel sessions.
T3 should start after T1's VERIFY_PASS is confirmed (T3 tests the teardown path T1 hardens).

---

*Spec written: 2026-09-04 | copier-spec mode | Director decisions recorded above*
*Source inspection: TradeCopierPanel.cs lines 577-624 (Detach), 1131-1237 (BuildBufferedButtonsRow + BuildArrowCluster)*
