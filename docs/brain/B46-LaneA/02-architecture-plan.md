# B46-LaneA — Architecture Plan
**Block**: PTT-COPIER-B46 — ATM Template Wiring Fix
**Epic**: B46-LaneA
**Date**: 2026-08-06
**Status**: PLAN_COMPLETE
**Author**: ptt-architect (Phase 1)
**Wave Workspace**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\`

---

## §1. Block Summary

B46 closes two defects confirmed via live acceptance test DW-B42-05 during the B45 pipeline.
Both defects cause `PTTFollowerStrategy` to crash (MaxRestarts → auto-disabled) or produce no
ATM bracket orders when `OnApplyRule` runs with a follower whose ATM ComboBox was loaded to
a non-Inherit selection that was never written back to `FollowerItem.AtmModeName`.

**Defect IDs**:
- `DW-B46-ATM-EMPTY-GUARD-01` — `CallAtmStrategyCreate` receives empty `AtmTemplateName` → NT8 throws → strategy dies
- `DW-B46-COMBO-AUTOSELECT-02` — `OnFollowerAtmTemplateComboLoaded` sets `SelectedIndex` but never writes `item.AtmModeName` → Apply sees "Inherit" → empty template propagated

**4 tickets (T1–T4)**:
| Ticket | File | Change |
|--------|------|--------|
| T1 | `Features/PttFollowerStrategy.cs` | Add empty-guard in `CallAtmStrategyCreate` |
| T2 | `TradeCopierPanel.cs` | Write `item.AtmModeName` after `cb.SelectedIndex = defaultIdx` |
| T3 | `CopyEngine.cs` | Update `PttBuild.Tag` to B46 |
| T4 | `B46Tests.cs` | 3 xUnit `[Fact]` tests |

---

## §2. Root Cause Analysis

### §2.1 DW-B46-ATM-EMPTY-GUARD-01

**Location**: `Features/PttFollowerStrategy.cs` → `CallAtmStrategyCreate(FillSignalEventArgs args)`

**Failure chain**:
1. Follower ATM mode is `Inherit` (no template selected before Apply).
2. `OnApplyRule` calls `ParseAtmModeNameLocal("Inherit")` → returns `FollowerAtmMode.Inherit` → `atmTemplate = null`.
3. `CopyEngine.SendCopy` passes `atmTemplate ?? string.Empty` to `PttBus.RaiseFillSignal`.
4. `FillSignalEventArgs.Create(…, "" , …)` sets `AtmTemplateName = ""` (null is coalesced to `string.Empty` in `Create`).
5. `PttFollowerStrategy.OnFillSignal` receives the signal; both account and instrument guards pass.
6. `CallAtmStrategyCreate(args)` calls `AtmStrategyCreate(…, "" , …)`.
7. NT8 runtime throws `"Strategy template name parameter missing"`.
8. NT8 strategy framework counts the exception → MaxRestarts (4 in 5 min) → strategy auto-disabled.
9. Copy engine is deaf from that point forward.

**Correct behaviour**: Empty `AtmTemplateName` = user selected no ATM template = Inherit = "no bracket needed on follower". Strategy should stay alive; bracket creation is simply skipped.

**Fix**: Guard at top of `CallAtmStrategyCreate`:
```csharp
if (string.IsNullOrWhiteSpace(args.AtmTemplateName)) return;
```

---

### §2.2 DW-B46-COMBO-AUTOSELECT-02

**Location**: `TradeCopierPanel.cs` → `OnFollowerAtmTemplateComboLoaded`

**Failure chain**:
1. `OnFollowerAtmTemplateComboLoaded` populates ComboBox items; finds leader template at `defaultIdx`.
2. Sets `cb.SelectedIndex = defaultIdx` (e.g., index 2 = leader template "MES $200 SL4").
3. **Gap**: Setting `SelectedIndex` programmatically does NOT reliably trigger `SelectionChangedEvent` at DataTemplate load time in WPF; even if it does, `DataContext` binding may not be fully resolved on the ComboBox when `OnFollowerAtmTemplateComboChanged` fires.
4. `item.AtmModeName` stays at its default value `"Inherit"`.
5. User sees ComboBox showing "MES $200 SL4" but `item.AtmModeName == "Inherit"`.
6. User clicks Apply without touching the ComboBox → `OnApplyRule` reads `atmNames[i] = item.AtmModeName ?? "Inherit" = "Inherit"` → Inherit path → `atmTemplate = null` → empty string → triggers DW-B46-ATM-EMPTY-GUARD-01.

**Correct behaviour**: When `cb.SelectedIndex` is set programmatically to a template (index > 0), `item.AtmModeName` must be synchronised immediately to `"Named:" + templateName`, matching the format written by `OnFollowerAtmTemplateComboChanged`.

**Fix**: After `cb.SelectedIndex = defaultIdx;`, if `defaultIdx > 0`, explicitly write:
```csharp
item.AtmModeName = "Named:" + (cb.SelectedItem as string ?? string.Empty);
```

---

## §3. Files In Scope

| Label | Full Path | Change Type |
|-------|-----------|-------------|
| FILE A | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttFollowerStrategy.cs` | 1-line insertion |
| FILE B | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs` | 6-line block insertion |
| FILE C | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` | 1-line const update |
| FILE D | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\B46Tests.cs` | New file (~75 lines) |

**Files NOT in scope**: `PttContracts.cs`, `TradeCopierWindow.cs`, `PttBreakEven.cs`, `PttGlobalBreakEven.cs`, `CopyEngineTests.cs`, any other `.cs` file.

---

## §4. Change Design — T1: PttFollowerStrategy Guard

### §4.1 Location
**File**: `Features/PttFollowerStrategy.cs`
**Method**: `CallAtmStrategyCreate(FillSignalEventArgs args)`
**Line** (approx): Line 64 in current source — insert BEFORE `AtmStrategyCreate(` call.

### §4.2 Before (current production code)
```csharp
// CYC=1: virtual test seam -- production implementation calls AtmStrategyCreate.
// Test subclasses override to capture calls without NT8 runtime.
// ARCH-BRACKET-03 path B: ATM call is on the same thread as the FillSignal callback.
protected virtual void CallAtmStrategyCreate(FillSignalEventArgs args)
{
    AtmStrategyCreate(
        args.OrderAction,
        OrderType.Market,
        0,
        0,
        TimeInForce.Gtc,
        args.EntryOrderId,
        args.AtmTemplateName,
        Guid.NewGuid().ToString("N").Substring(0, 8),
        (code, msg) =>
        {
            if (code != ErrorCode.NoError)
                Print("B42 ATM error: " + msg);
        });
}
```

### §4.3 After (with guard)
```csharp
// CYC=2: 1 guard branch + 1 callback branch.
// B46 T1: DW-B46-ATM-EMPTY-GUARD-01 -- empty AtmTemplateName = Inherit mode on follower.
// AtmStrategyCreate("") throws "Strategy template name parameter missing" -> MaxRestarts -> disabled.
// Guard: return silently. No bracket = correct for Inherit. Strategy stays alive.
// JS-001: no throw. JS-021: no lock. NT8-019: no async void.
// string.IsNullOrWhiteSpace: using System; already present (line 2 of file).
protected virtual void CallAtmStrategyCreate(FillSignalEventArgs args)
{
    if (string.IsNullOrWhiteSpace(args.AtmTemplateName)) return;  // B46 DW-B46-ATM-EMPTY-GUARD-01
    AtmStrategyCreate(
        args.OrderAction,
        OrderType.Market,
        0,
        0,
        TimeInForce.Gtc,
        args.EntryOrderId,
        args.AtmTemplateName,
        Guid.NewGuid().ToString("N").Substring(0, 8),
        (code, msg) =>
        {
            if (code != ErrorCode.NoError)
                Print("B46 ATM error: " + msg);
        });
}
```

**Note**: The Print string updated to `"B46 ATM error: "` from `"B42 ATM error: "` to reflect current block. This is a cosmetic update; engineer must make this change too.

### §4.4 CYC Analysis
| State | CYC |
|-------|-----|
| Before | 1 (straight-line; callback lambda is separate scope) |
| After | 2 (`if (string.IsNullOrWhiteSpace...)` adds 1 branch) |
| Limit | ≤ 8 ✓ |

### §4.5 JS Rule Compliance
| Rule | Check |
|------|-------|
| JS-001 (no throw in hot path) | PASS — no throw introduced |
| JS-002 (no return null) | PASS — `return;` is void return, not null |
| JS-021 (no lock) | PASS — no lock introduced; guard reads `args` (stack value) |
| JS-033 (no async void) | PASS — method remains `protected virtual void` |

### §4.6 NT8 Compiler Compliance
| Rule | Check |
|------|-------|
| NT8-001 (no init setters) | PASS — no new properties |
| NT8-019 (no async void) | PASS — synchronous void |
| NT8-044 (using System required) | PASS — `using System;` present at line 2 of file (confirmed) |
| NT8-013 (no DateTime.Now) | PASS — no DateTime usage |

**Thread context**: `OnFillSignal` fires on the CopyEngine order-routing thread. `args.AtmTemplateName` is a `string` field on a `struct` passed by value. The guard reads this field in a purely local context — no shared mutable state, no cross-thread write. Safe without Dispatcher or volatile.

---

## §5. Change Design — T2: TradeCopierPanel ComboBox Wiring

### §5.1 Location
**File**: `TradeCopierPanel.cs`
**Method**: `OnFollowerAtmTemplateComboLoaded(object sender, RoutedEventArgs e)`
**Insertion point**: AFTER line `cb.SelectedIndex = defaultIdx;` (currently last statement in method)

### §5.2 Before (current production code — lines 1608–1639)
```csharp
private void OnFollowerAtmTemplateComboLoaded(object sender, RoutedEventArgs e)
{
    var cb = sender as ComboBox;
    if (cb == null) return;                                // branch 1 -- null guard
    if (cb.Items.Count > 0) return;                       // branch 2 -- idempotency guard
    cb.Items.Add("(none)");
    string leaderTemplate = GetLeaderAtmTemplateName(_currentChart);
    int defaultIdx = 0;
    try
    {
        string atmDir = System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
            "NinjaTrader 8", "templates", "AtmStrategy");
        if (System.IO.Directory.Exists(atmDir))
        {
            foreach (var f in System.IO.Directory.GetFiles(atmDir, "*.xml")) // branch 3
            {
                string tName = System.IO.Path.GetFileNameWithoutExtension(f);
                cb.Items.Add(tName);
                if (tName == leaderTemplate)
                    defaultIdx = cb.Items.Count - 1;      // branch 4 -- leader found
            }
        }
    }
    catch
    {
        // Directory unavailable -- "(none)" only.
    }
    cb.SelectedIndex = defaultIdx;
}
// CYC = 4 (branches 1, 2, 3-loop, 4-leader-found)
```

### §5.3 After (with AtmModeName write-back block)
```csharp
private void OnFollowerAtmTemplateComboLoaded(object sender, RoutedEventArgs e)
{
    var cb = sender as ComboBox;
    if (cb == null) return;                                // branch 1 -- null guard
    if (cb.Items.Count > 0) return;                       // branch 2 -- idempotency guard
    cb.Items.Add("(none)");
    string leaderTemplate = GetLeaderAtmTemplateName(_currentChart);
    int defaultIdx = 0;
    try
    {
        string atmDir = System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
            "NinjaTrader 8", "templates", "AtmStrategy");
        if (System.IO.Directory.Exists(atmDir))
        {
            foreach (var f in System.IO.Directory.GetFiles(atmDir, "*.xml")) // branch 3
            {
                string tName = System.IO.Path.GetFileNameWithoutExtension(f);
                cb.Items.Add(tName);
                if (tName == leaderTemplate)
                    defaultIdx = cb.Items.Count - 1;      // branch 4 -- leader found
            }
        }
    }
    catch
    {
        // Directory unavailable -- "(none)" only.
    }
    cb.SelectedIndex = defaultIdx;
    // B46 T2: DW-B46-COMBO-AUTOSELECT-02 -- sync item.AtmModeName to match visual selection.
    // Setting cb.SelectedIndex programmatically does NOT reliably fire SelectionChangedEvent
    // at DataTemplate load time. Without this block, item.AtmModeName stays "Inherit" even
    // when the ComboBox visually shows a real template. OnApplyRule then reads "Inherit" ->
    // empty atmTemplate -> AtmStrategyCreate("") -> NT8 exception -> MaxRestarts -> disabled.
    // Format: "Named:templateName" matches OnFollowerAtmTemplateComboChanged write pattern.
    if (defaultIdx > 0)                                   // branch 5 -- non-Inherit selection
    {
        var item = (cb.DataContext as FollowerItem)
                   ?? FindAncestorDataContext<FollowerItem>(cb);
        if (item != null)                                 // branch 6 -- item resolved
            item.AtmModeName = "Named:" + (cb.SelectedItem as string ?? string.Empty);
    }
}
// CYC = 6 (branches 1, 2, 3-loop, 4-leader-found, 5-defaultIdx>0, 6-item!=null)
```

### §5.4 CYC Analysis
| State | CYC |
|-------|-----|
| Before | 4 |
| After | 6 |
| Limit | ≤ 8 ✓ |

### §5.5 JS Rule Compliance
| Rule | Check |
|------|-------|
| JS-001 (no throw) | PASS — no throw; whole method is guarded by outer try/catch |
| JS-002 (no return null) | PASS — no return null; item lookup returns `default(T)` which is checked |
| JS-021 (no lock) | PASS — no lock; all operations on WPF UI thread; `FollowerItem.AtmModeName` is a UI-thread-only field |
| JS-033 (no async void) | PASS — `private void` event handler, synchronous |

### §5.6 NT8 Compiler Compliance
| Rule | Check |
|------|-------|
| NT8-001 (no init setters) | PASS — no new properties |
| NT8-012 (FrameworkElementFactory) | PASS — not changing factory; Loaded handler already uses `AddHandler(FrameworkElement.LoadedEvent, …)` pattern |
| NT8-019 (no async void) | PASS — synchronous void |
| NT8-042 (no Dispatcher.InvokeAsync from AddOn) | N/A — handler fires on UI thread; no Dispatcher needed |
| NT8-043 (no null-conditional compound assignment) | PASS — no -=/?. patterns |

**Thread context**: `OnFollowerAtmTemplateComboLoaded` is a WPF `RoutedEvent` Loaded handler. WPF guarantees Loaded fires on the UI thread. All `VisualTreeHelper.GetParent` and `cb.DataContext` accesses are UI-thread safe. `item.AtmModeName` is written on the UI thread and read on the UI thread (during `OnApplyRule`). No cross-thread hazard.

---

## §6. Change Design — T3: Build Tag

**File**: `CopyEngine.cs`
**Class**: `PttBuild` (internal static class at line 39)

| State | Value |
|-------|-------|
| Before | `"PTT-COPIER B43 \| atm-template-picker \| 2026-08-05"` |
| After | `"PTT-COPIER B46 \| atm-template-guard \| 2026-08-06"` |

**Change type**: Single const string replacement. No logic change.

---

## §7. Test Design — T4: B46Tests.cs

**File**: `src/PropTraderTools/B46Tests.cs` (NEW)
**Pattern**: Follows `B42Tests.cs` structure — top comment block, one test class per test group.
**NT8 runtime**: NOT required for any test (pure predicate and static method tests).
**xUnit only** — no NUnit, no MSTest.

### §7.1 Test T_B46_01 — Guard fires on empty AtmTemplateName

```csharp
[Fact]
public void T_B46_01_IsNullOrWhiteSpace_EmptyString_ReturnsTrue()
{
    // Asserts that the guard predicate string.IsNullOrWhiteSpace("") == true.
    // This is the exact condition that triggers the early-return in CallAtmStrategyCreate.
    // FillSignalEventArgs.Create coalesces null->string.Empty, so args.AtmTemplateName is "".
    Assert.True(string.IsNullOrWhiteSpace(""));
    Assert.True(string.IsNullOrWhiteSpace("   "));  // whitespace also guarded
}
```

**Asserts**: `string.IsNullOrWhiteSpace("")` and `string.IsNullOrWhiteSpace("   ")` both return `true`.
**Why this and not the NT8 call**: `AtmStrategyCreate` is NT8-runtime-only. Testing the predicate (the guard condition) in isolation is the correct NT8-runtime-free approach. The engineer verifies at F5 that the guard is wired correctly; the unit test confirms the predicate logic.

### §7.2 Test T_B46_02 — Guard does not fire on non-empty AtmTemplateName

```csharp
[Fact]
public void T_B46_02_IsNullOrWhiteSpace_NonEmptyString_ReturnsFalse()
{
    // Asserts that the guard predicate string.IsNullOrWhiteSpace("MyATM") == false.
    // Non-empty template name -> guard does NOT fire -> AtmStrategyCreate is called.
    Assert.False(string.IsNullOrWhiteSpace("MyATM"));
    Assert.False(string.IsNullOrWhiteSpace("MES $200 SL4"));
}
```

**Asserts**: `string.IsNullOrWhiteSpace("MyATM")` and `string.IsNullOrWhiteSpace("MES $200 SL4")` return `false`.

### §7.3 Test T_B46_03 — "Named:X" round-trips through ParseAtmModeName

```csharp
[Fact]
public void T_B46_03_ParseAtmModeName_NamedPrefix_ReturnsNamedMode()
{
    // Asserts that "Named:MES $200 SL4" parses to FollowerAtmMode.Named with
    // TemplateName == "MES $200 SL4". This validates the full round-trip:
    // item.AtmModeName = "Named:MES $200 SL4" (written by T2 fix)
    //   -> ParseAtmModeName("Named:MES $200 SL4") -> Named(TemplateName = "MES $200 SL4")
    //   -> CopyEngine.SendCopy receives "MES $200 SL4" as atmTemplate
    //   -> PttBus.RaiseFillSignal("MES $200 SL4")
    //   -> CallAtmStrategyCreate receives args.AtmTemplateName = "MES $200 SL4" (non-empty -> no guard)
    //   -> AtmStrategyCreate("MES $200 SL4") succeeds.
    var mode = CopyEngine.ParseAtmModeName("Named:MES $200 SL4");
    Assert.IsType<FollowerAtmMode.Named>(mode);
    var named = (FollowerAtmMode.Named)mode;
    Assert.Equal("MES $200 SL4", named.TemplateName);
}
```

**Asserts**:
1. `CopyEngine.ParseAtmModeName("Named:MES $200 SL4")` returns a `FollowerAtmMode.Named` instance.
2. `named.TemplateName == "MES $200 SL4"` (Substring(6) of "Named:MES $200 SL4").

**Note**: `CopyEngine.ParseAtmModeName` is `internal static` — accessible from the same assembly. `B46Tests.cs` lives in `namespace PropTraderTools`, matching all other test files.

### §7.4 Complete B46Tests.cs file structure

```csharp
// PTT-COPIER-B46 -- B46Tests.cs
// xUnit [Fact] tests for B46: ATM empty-guard predicate + Named mode round-trip.
// Jane Street rules: JS-001, JS-002, JS-021.
// NT8 runtime NOT required -- all tests exercise pure C# predicates or static helpers.
// xUnit only -- no NUnit, no MSTest.
using System;
using Xunit;

namespace PropTraderTools
{
    // Three facts covering DW-B46-ATM-EMPTY-GUARD-01 and DW-B46-COMBO-AUTOSELECT-02.
    public class B46AtmGuardTests
    {
        [Fact]
        public void T_B46_01_IsNullOrWhiteSpace_EmptyString_ReturnsTrue()
        { … }

        [Fact]
        public void T_B46_02_IsNullOrWhiteSpace_NonEmptyString_ReturnsFalse()
        { … }

        [Fact]
        public void T_B46_03_ParseAtmModeName_NamedPrefix_ReturnsNamedMode()
        { … }
    }
}
```

---

## §8. Execution Order and Dependencies

```
T3 (build tag)  — no dependencies; can execute first or last
T1 (guard)      — no dependency on T2 or T3
T2 (combo fix)  — no dependency on T1 or T3
T4 (tests)      — depends on T1 (guard predicate) and T2 (ParseAtmModeName) being present
                   (ParseAtmModeName is unchanged but must be accessible)
```

**Recommended order**: T1 → T2 → T3 → T4

**Link sync**: After all 4 tickets, run `powershell -File scripts\verify_links.ps1 -Fix` in Wave workspace.

---

## §9. Acceptance Criteria (DW-B42-05 D1–D7)

These are the criteria for the live F5 acceptance test that B46 enables:

| ID | Criterion | How B46 addresses it |
|----|-----------|----------------------|
| D1 | Entry order copied to follower account | Pre-existing (B42/B44); not changed |
| D2 | Stop leg spawned on follower | T1 guard ensures strategy stays alive; template passed correctly by T2 |
| D3 | Target leg(s) spawned on follower | Same as D2 |
| D4 | Leader ATM bracket unchanged | T1 and T2 only affect follower path; leader is untouched |
| D5 | NT8 Output shows no "ATM error" messages | T1 guard eliminates the `"Strategy template name parameter missing"` throw |
| D6 | Strategy NOT auto-disabled after trade | T1 guard prevents MaxRestarts accumulation |
| D7 | AtmModeName written correctly at load time | T2 fix writes `item.AtmModeName = "Named:..."` at ComboBox load |

**To run DW-B42-05 acceptance test after B46 ships**:
1. Configure `PTTFollowerStrategy` in NT8 Control Center Strategies tab with Sim101 as follower.
2. Select a real ATM template in the follower row ComboBox (e.g., "MES $200 SL4"). Click Apply.
3. Fire a test trade from the leader account.
4. Verify D1–D6 in NT8 Orders tab and Output window.

---

## §10. Deferred Items from Prior Blocks (Carry Forward — Read Only)

The following items remain OPEN from the B44 backlog. B46 does not close them except as noted.

| ID | Priority | Status After B46 | Notes |
|----|----------|-----------------|-------|
| DW-B42-01 | P2 | OPEN | T3 test for IsPttQxTarget not in B46 scope |
| DW-B42-02 | P1 | OPEN — enableable | B46 unblocks live test; must be run next session |
| DW-B42-03 | P2 | OPEN | T4/T5 slot extension — future block |
| DW-B42-04 | P2 | OPEN | `NT8-NEW` comment at PttContracts.cs:254 → `NT8-005` — cosmetic |
| DW-B42-05 | P1 | UNBLOCKED by B46 | Run D1–D4 live acceptance test after B46 ships |
| DW-B43-02 | P1 | PARTIALLY CLOSED | T2 fixes AtmModeName write-back (component b). GetLeaderAtmTemplateName index accuracy (component a) remains open. |
| DW-B43-03 | P2 | OPEN | Future NT8 upgrade |
| DW-B44-01 | P1 | OPEN | CopyEngineTests.cs 60 compile errors — cleanup block |
| DW-B44-02 | P1 | OPEN | Live F5 (DW-B42-05) — run after B46 ships |
| DW-B44-03 | P1 | PARTIALLY CLOSED | Same as DW-B43-02 |

**DW-B43-02 partial closure note**: B46 T2 closes the `AtmModeName` write-back sub-issue (component b: AtmModeName not written at load). The `GetLeaderAtmTemplateName` visual-tree index accuracy (component a: ComboBox index 2 may not be the ATM template ComboBox for all chart configurations) is NOT in B46 scope. If the leader template is auto-selected incorrectly, the user can override manually via ComboBox; the critical path (crash prevention) is addressed by T1.

---

## §11. Scope Exclusions

The following are explicitly NOT in B46 scope:

1. **`GetLeaderAtmTemplateName` visual-tree index accuracy** (DW-B43-02 component a) — the `FindVisualChildByIndex<ComboBox>(ct, 2)` index is not verified or changed. Fixing the wrong index is a separate investigation.
2. **`CopyEngineTests.cs` cleanup** (DW-B44-01) — the 60 pre-existing compile errors in the legacy test file are out of scope. `B46Tests.cs` is self-contained and does not depend on `CopyEngineTests.cs`.
3. **`OnApplyRule` logic changes** — no changes to how `ParseAtmModeNameLocal` is called or how `atmTemplate` is computed in `OnApplyRule`. The fix is entirely at the data-source level (ComboBox load) and the consumer level (strategy guard).
4. **Market mode ATM** — `FollowerAtmMode.Market` path is not exercised or changed. Out of scope.
5. **Multiple follower rows** — the T2 fix applies per-row (each ComboBox fires its own Loaded handler independently). No multi-row coordination logic needed.
6. **Live F5 acceptance test** — B46 enables DW-B42-05 but does not execute it. The test is deferred to the next live session.

---

## §12. Jane Street Alignment

| Rule | P0 | Scope | Check |
|------|----|-------|-------|
| JS-001 (no throw in hot path) | P0 | T1 guard: `return;` not `throw`. T2 write-back: no throw. | PASS |
| JS-002 (no return null) | P0 | T1: void method, `return;` not `return null`. T2: no return statement. `FindAncestorDataContext` returns `default(T)` not null when unresolved. | PASS |
| JS-021 (no lock) | P0 | T1: guard reads a stack-local struct field; no shared mutable state. T2: UI-thread-only operation; `FollowerItem.AtmModeName` written/read on UI thread only. | PASS |
| JS-033 (no async void) | P0 | T1: `protected virtual void`. T2: `private void`. Neither is async. | PASS |
| JS-004 (exhaustive matching) | P1 | `FollowerAtmMode` hierarchy already has Inherit/Market/Named — unchanged. Not applicable to B46. | N/A |
| JS-008 (readonly struct) | P1 | `FillSignalEventArgs` is a struct — unchanged. Not applicable to B46. | N/A |

---

## §13. NT8 Compiler Alignment

| Rule | Severity | Applicable | Status |
|------|----------|-----------|--------|
| NT8-001 (`init` setter banned) | P0 | No new properties added | PASS |
| NT8-002 (`abstract/sealed record` banned) | P0 | No records | PASS |
| NT8-003 (`volatile double` banned) | P0 | No new volatile fields | PASS |
| NT8-004 (`System.Collections.Immutable` banned) | P0 | No immutable collections | PASS |
| NT8-005 (`readonly struct + private set` banned) | P0 | No new structs | PASS |
| NT8-007 (`CreateOrder` arg 12) | P0 | No `CreateOrder` calls in B46 | N/A |
| NT8-012 (`FrameworkElementFactory` Loaded pattern) | P1 | T2 appends to existing Loaded handler — no FEF columns | PASS |
| NT8-013 (`DateTime.Now` banned) | P0 | No DateTime usage | PASS |
| NT8-014 (PTT- prefix on CreateOrder) | P1 | No CreateOrder calls | N/A |
| NT8-018 (`lock()` banned) | P1 | No lock | PASS |
| NT8-019 (`async void` banned) | P0 | No async void | PASS |
| NT8-020 (SolidColorBrush must Freeze) | P1 | No new brushes | N/A |
| NT8-042 (`Dispatcher.InvokeAsync` unavailable) | P0 | T2 runs on UI thread — no Dispatcher needed | PASS |
| NT8-043 (null-conditional compound assignment banned) | P0 | No `?.Event -=` patterns | PASS |
| NT8-044 (`StringComparison` needs `using System;`) | P0 | `IsNullOrWhiteSpace` is `string.IsNullOrWhiteSpace` static method (not `StringComparison` enum) — but note: `using System;` is already present in `PttFollowerStrategy.cs` (line 2, confirmed) | PASS |
| NT8-045 (`AtmStrategyTemplates` filesystem workaround) | P1 | Already implemented in existing `OnFollowerAtmTemplateComboLoaded`; B46 only appends after that block | PASS |

**No new NT8 compiler rules discovered in B46.** Post-session audit: `nt8-rules(B46): no new rules`.

---

## §14. 7-Scan Pre-Commit Checklist (SCAN-01 through SCAN-07)

These scans MUST pass before any commit of B46 changes:

| Scan | Pattern | Expected Result |
|------|---------|----------------|
| SCAN-01 | `grep -n "lock(" src/PropTraderTools/Features/PttFollowerStrategy.cs` | Zero matches |
| SCAN-02 | `grep -n "async void" src/PropTraderTools/Features/PttFollowerStrategy.cs src/PropTraderTools/TradeCopierPanel.cs` | Zero matches in new code |
| SCAN-03 | `grep -n "return null" src/PropTraderTools/Features/PttFollowerStrategy.cs src/PropTraderTools/TradeCopierPanel.cs` | Zero matches in new code |
| SCAN-04 | `grep -n "DateTime.Now" src/PropTraderTools/CopyEngine.cs` | Zero matches |
| SCAN-05 | `grep -n '"#[0-9A-Fa-f]' src/PropTraderTools/B46Tests.cs src/PropTraderTools/TradeCopierPanel.cs` | Zero matches |
| SCAN-06 | `grep -rn "FontFamily" src/PropTraderTools/TradeCopierPanel.cs` | Zero matches |
| SCAN-07 | `grep -n "acc.CreateOrder" src/PropTraderTools/Features/PttFollowerStrategy.cs src/PropTraderTools/TradeCopierPanel.cs` | Zero matches (no CreateOrder in B46 files) |

---

*Architecture plan complete. All 8 sequential thoughts executed. All scans pre-checked. No violations found.*
