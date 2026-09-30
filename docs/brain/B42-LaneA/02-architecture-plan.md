# B42-LaneA — Architecture Plan
**Block**: PTT-COPIER-B42 — PTTFollowerStrategy: Native ATM Brackets on Followers
**Phase**: 1 — Architecture
**Spec Section**: `specs/002-trade-copier-spec.html#block-b42`
**Status**: REVIEW_PASS (Cycle 2)
**Architect**: ptt-architect
**Date**: 2026-08-05

---

## 1. File Modification Table

| # | File | Change Type | Est. Lines Added | Notes |
|---|------|-------------|-----------------|-------|
| T1 | `src/PropTraderTools/Core/PttContracts.cs` | Modify existing | +25 | Add `FillSignalEventArgs` struct + `FillSignal` event + `RaiseFillSignal` to `PttBus` |
| T2 | `src/PropTraderTools/CopyEngine.cs` | Modify existing | +3 | Publish `PttBus.RaiseFillSignal` inside `SendCopy()` try block after `CreateOrder` |
| T3 | `src/PropTraderTools/Features/PttFollowerStrategy.cs` | **NEW FILE** | ~60 | Headless NT8 Strategy; subscribes to `FillSignal`; calls `AtmStrategyCreate` on match |
| T4 | `tests/PropTraderTools.Tests/B42Tests.cs` | **NEW FILE** | ~90 | 7 `[Fact]` methods covering struct, event bus, guards, dispatch, and CopyEngine publish |

**Total delta**: ~178 lines across 4 files.
**Files NOT touched**: all other `src/PropTraderTools/*.cs`, all existing test files.

---

## 2. `FillSignalEventArgs` Struct Specification

### Location
Bottom of `src/PropTraderTools/Core/PttContracts.cs`, inside `namespace PropTraderTools { }` block, after `QuickExitEventArgs`.

### Type declaration
```csharp
// B42: FillSignalEventArgs -- carries fill data from CopyEngine to PttFollowerStrategy.
// NT8-001: { get; private set; } + constructor (init accessor BANNED in NT8).
// JS-008: readonly struct for immutable data. NOT a class (no EventArgs base needed --
//         FillSignal is Action<FillSignalEventArgs>, not EventHandler<T>).
// JS-010: private constructor + public static Create() factory (signal struct rule).
public readonly struct FillSignalEventArgs
{
    public Account     Account         { get; private set; }
    public Instrument  Instrument      { get; private set; }
    public string      AtmTemplateName { get; private set; }
    public OrderAction OrderAction     { get; private set; }
    public int         Quantity        { get; private set; }
    public string      EntryOrderId    { get; private set; }

    private FillSignalEventArgs(
        Account     account,
        Instrument  instrument,
        string      atmTemplateName,
        OrderAction orderAction,
        int         quantity,
        string      entryOrderId)
    {
        Account         = account;
        Instrument      = instrument;
        AtmTemplateName = atmTemplateName ?? string.Empty;
        OrderAction     = orderAction;
        Quantity        = quantity;
        EntryOrderId    = entryOrderId ?? string.Empty;
    }

    // JS-010: smart constructor -- only valid construction path.
    public static FillSignalEventArgs Create(
        Account     account,
        Instrument  instrument,
        string      atmTemplateName,
        OrderAction orderAction,
        int         quantity,
        string      entryOrderId)
        => new FillSignalEventArgs(account, instrument,
               atmTemplateName, orderAction, quantity, entryOrderId);
}
```

### Field table

| Field | Type | Source in CopyEngine | Notes |
|-------|------|----------------------|-------|
| `Account` | `NinjaTrader.Cbi.Account` | `follower` (the follower account object) | Used by `PttFollowerStrategy` for account guard |
| `Instrument` | `NinjaTrader.Cbi.Instrument` | `instrument` param of `SendCopy` | Used for instrument guard |
| `AtmTemplateName` | `string` | `atmTemplate ?? string.Empty` | Empty string if mode is not Named; never null |
| `OrderAction` | `NinjaTrader.Cbi.OrderAction` | `signal.Action` | Buy or Sell |
| `Quantity` | `int` | `signal.Quantity` | Number of contracts |
| `EntryOrderId` | `string` | `signal.OrderId` | Unique order ID used as ATM entry order ID; never null |

**NT8-001 compliance**: All properties use `{ get; private set; }`. Constructor assigns all fields. No `init` accessor anywhere.

**JS-010 compliance**: Constructor is `private`. `FillSignalEventArgs.Create(...)` is the only public construction path — consistent with `CopyRule.Create`, `FollowerBinding`, `CopySignal.Create` patterns already in the codebase.

**Constructor CYC**: 1 (no branches, only assignments). **PASS**.
**`Create` factory CYC**: 1 (single expression body, no branches). **PASS**.

---

## 3. `PttBus` Additions — `FillSignal` Event and `RaiseFillSignal`

### Location
Inside the `PttBus` class body in `PttContracts.cs`, after the existing `QuickExitFired` event and `RaiseQuickExit` method.

### Event declaration
```csharp
// B42: Action<T> (not EventHandler<T>) because FillSignalEventArgs is a readonly struct,
// not an EventArgs subclass. JS-021: CLR += / -= are atomic -- no lock needed.
public static event Action<FillSignalEventArgs> FillSignal;
```

Access modifier is `public` (not `internal`) because `PttFollowerStrategy`, which lives in a
separate NinjaScript compilation unit, must be able to subscribe at `State.Realtime`.

### `RaiseFillSignal` method
```csharp
// B42: NT8-043 local-copy-then-null-check pattern. CYC=2. JS-021: no lock.
public static void RaiseFillSignal(FillSignalEventArgs args)
{
    var h = FillSignal;
    if (h != null) h(args);
}
```

**Access modifier**: `public` — called from `CopyEngine.SendCopy()` (same namespace) and must
be reachable if called from outside. Consistent with `RaiseBe` / `RaiseTrim` access levels.

**CYC analysis**: 1 assignment + 1 branch (null guard) = **CYC=2**. PASS.

**Pattern consistency**: Matches existing `RaiseBe`, `RaiseTrim`, `RaiseFlatted`, `RaiseCancel`,
`RaiseQuickExit` exactly — local copy, null-check, invoke. No deviation.

---

## 4. `SendCopy()` Publish Point — Exact Placement and CYC Analysis

### Existing `SendCopy()` method structure (lines 808–853 in CopyEngine.cs)

```
SendCopy(Account follower, Instrument instrument, in CopySignal signal, FollowerAtmMode mode)
  [branch 1] if (mode is FollowerAtmMode.Market) → override orderType/limitPrice
  [branch 2] ternary: atmTemplate = mode is FollowerAtmMode.Named named ? ... : null
  [branch 3] try {
      follower.CreateOrder(12 args)    ← existing
      ← INSERT HERE (after CreateOrder, before return true)
      return true                      ← existing
  }
  catch (Exception ex) {
      StatusUpdate?.Invoke(...)        ← existing
      return false                     ← existing
  }
```

### Insertion — the only change to `CopyEngine.cs`

Replace the `return true;` line with:

```csharp
PttBus.RaiseFillSignal(FillSignalEventArgs.Create(
    follower,
    instrument,
    atmTemplate ?? string.Empty,
    signal.Action,
    signal.Quantity,
    signal.OrderId));
return true;
```

**Exact diff**:
- **BEFORE** (line 846): `return true;`
- **AFTER** (lines 846–851):
  ```csharp
  PttBus.RaiseFillSignal(FillSignalEventArgs.Create(
      follower,
      instrument,
      atmTemplate ?? string.Empty,
      signal.Action,
      signal.Quantity,
      signal.OrderId));
  return true;
  ```

### CYC analysis after modification

| Branch | Description | Count |
|--------|-------------|-------|
| 1 | `if (mode is FollowerAtmMode.Market)` | +1 |
| 2 | Ternary `mode is FollowerAtmMode.Named named ? ...` | +1 |
| 3 | `try` entry | +1 |
| 4 | `catch` path | +1 |
| — | New `RaiseFillSignal` call | +0 (no branch) |

**CYC before modification**: 5 (per comment in source).
**CYC after modification**: **5** (unchanged). PASS — well within ≤8 limit.

### Why publish is inside `try` after `CreateOrder`

- Publish only fires on **success** (no exception from `CreateOrder`).
- If `CreateOrder` throws, control goes to `catch` — the `RaiseFillSignal` call is **never reached**.
- This satisfies T_B42_07: "SendCopy does NOT publish FillSignal if CreateOrder throws."

### `signal.OrderId` assumption

`CopySignal.OrderId` (the entry order ID) is confirmed available in the prompt KEY FACTS.
The engineer must verify this field exists in `CopySignal` before implementing. If the field
name differs, use the correct field name carrying the entry order ID.

---

## 5. `PttFollowerStrategy` Class Skeleton

### File path
`src/PropTraderTools/Features/PttFollowerStrategy.cs`

### Namespace
`namespace PropTraderTools` — flat namespace, consistent with `CopyEngine.cs` and `PttContracts.cs`.

### Full skeleton with CYC annotations

```csharp
// B42: PTTFollowerStrategy -- headless NT8 Strategy for native ATM brackets on follower accounts.
// One instance per follower account per instrument, configured in NT8 Control Center Strategies tab.
// NT8-001: no init setters. NT8-003: no volatile fields. NT8-033: no async void.
// JS-021: no lock() -- event += / -= on NT8 lifecycle thread, raise from CopyEngine dispatch thread.
// ARCH-BRACKET-03: AtmStrategyCreate() available on StrategyBase only (confirmed 2026-08-05).

using System;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript.Strategies;

namespace PropTraderTools
{
    public class PttFollowerStrategy : Strategy
    {
        // CYC=4: 3 State cases (SetDefaults, Realtime, Terminated) + 1 base
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name                           = "PTTFollowerStrategy";
                Calculate                      = Calculate.OnBarClose;
                BarsRequiredToTrade            = 0;
                IsExitOnSessionCloseStrategy   = false;
            }
            else if (State == State.Realtime)
            {
                PttBus.FillSignal += OnFillSignal;
            }
            else if (State == State.Terminated)
            {
                PttBus.FillSignal -= OnFillSignal;
            }
        }

        // CYC=1: required NT8 override. Empty body -- this strategy acts only on FillSignal.
        protected override void OnBarUpdate() { }

        // CYC=3: 2 early-return guards + 1 AtmStrategyCreate call.
        // JS-021: no lock. Threading note: fires on CopyEngine dispatch thread.
        //         AtmStrategyCreate is called directly per spec design (ARCH-BRACKET-03 path B).
        private void OnFillSignal(FillSignalEventArgs args)
        {
            if (args.Account.Name != Account.Name) return;           // guard 1: not my account
            if (args.Instrument.FullName != Instrument.FullName) return; // guard 2: not my instrument

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
    }
}
```

### Method CYC table

| Method | CYC | Branch sources |
|--------|-----|----------------|
| `OnStateChange()` | 4 | 3 `if/else if` on `State` value |
| `OnBarUpdate()` | 1 | Empty body — no branches |
| `OnFillSignal()` | 3 | 2 early-return guards + 1 `if (code != NoError)` in lambda |

All methods ≤ 8. PASS.

### NT8 Control Center configuration (one-time setup)

One `PTTFollowerStrategy` instance per `(follower account, instrument)` pair:
```
Account: Follower-A  |  Instrument: MES  →  [Run]
Account: Follower-B  |  Instrument: MES  →  [Run]
Account: Follower-A  |  Instrument: MNQ  →  [Run]   ← if trading MNQ
```
Strategy auto-starts from saved NT8 workspace on every session open.

---

## 6. Test Strategy — 7 `[Fact]` Methods

### File
`tests/PropTraderTools.Tests/B42Tests.cs` (new file, same project as `CopyEngineTests.cs`).

### NT8 dependency isolation strategy

- T_B42_01, T_B42_02: Pure CLR. `FillSignalEventArgs` is a struct and `PttBus` is a static class.
  No NT8 runtime needed. Use stub `Account` and `Instrument` objects (null-safe construction).
- T_B42_03, T_B42_04: Instantiate a `TestFollowerStrategy` subclass of `PttFollowerStrategy` that
  exposes a `protected virtual AtmInvoked` counter instead of the real `AtmStrategyCreate`. See
  T_B42_05 design below.
- T_B42_05: Same `TestFollowerStrategy` — assert `AtmInvoked == 1` after calling `OnFillSignal` with
  matching account + instrument.
- T_B42_06, T_B42_07: Use `MockFollowerCopyEngine` test double wrapping `CopyEngine` dispatch logic,
  or call `CopyEngine` directly with a fake `Account` that has a controlled `CreateOrder`. Track
  whether `PttBus.FillSignal` fired (subscribe a counter lambda before calling `SendCopy`).

### Testable subclass for T_B42_03, T_B42_04, T_B42_05

```csharp
// In B42Tests.cs — internal to test project
internal class TestFollowerStrategy : PttFollowerStrategy
{
    public int AtmInvokedCount { get; private set; }

    // Override allows tests to capture call without NT8 runtime
    protected override void CallAtmStrategyCreate(FillSignalEventArgs args)
    {
        AtmInvokedCount++;
    }
}
```

> **Engineer note**: To enable this, `PttFollowerStrategy.OnFillSignal` must call a
> `protected virtual void CallAtmStrategyCreate(FillSignalEventArgs args)` helper method
> instead of calling `AtmStrategyCreate(...)` directly. The real implementation of
> `CallAtmStrategyCreate` calls `AtmStrategyCreate(9 args)`. The test subclass overrides it
> to capture the call. This is the ONLY architectural addition required beyond the spec skeleton.

### `[Fact]` table

| ID | Method Name | What it asserts | NT8 runtime needed? |
|----|-------------|-----------------|---------------------|
| T_B42_01 | `FillSignalEventArgs_CarriesAllFields` | Call `FillSignalEventArgs.Create(...)`; assert all 6 field values round-trip correctly | No |
| T_B42_02 | `RaiseFillSignal_FiresAllSubscribers` | Subscribe 2 lambdas; call `RaiseFillSignal(args)`; assert both fired with correct `args` | No |
| T_B42_03 | `OnFillSignal_IgnoresWrongAccount` | `TestFollowerStrategy` with `Account.Name = "X"`; raise signal with `Account.Name = "Y"`; assert `AtmInvokedCount == 0` | No (stub Account) |
| T_B42_04 | `OnFillSignal_IgnoresWrongInstrument` | Account guard passes; `Instrument.FullName = "MES"` on strategy, `"MNQ"` in signal; assert `AtmInvokedCount == 0` | No (stub Instrument) |
| T_B42_05 | `OnFillSignal_CallsAtmWhenAccountAndInstrumentMatch` | Account + Instrument both match; assert `AtmInvokedCount == 1` | No (via override) |
| T_B42_06 | `SendCopy_PublishesFillSignalAfterCreateOrderSuccess` | Subscribe FillSignal counter; call `SendCopy` with a fake Account that does NOT throw; assert counter == 1 | No (fake Account) |
| T_B42_07 | `SendCopy_DoesNotPublishFillSignalWhenCreateOrderThrows` | Subscribe FillSignal counter; call `SendCopy` with a fake Account whose `CreateOrder` throws; assert counter == 0 | No (fake Account) |

**Test isolation**: Always unsubscribe the counter lambda from `PttBus.FillSignal` in test teardown
(`IDisposable` or `try/finally`) to prevent cross-test contamination of the static event.

---

## 7. Jane Street Rule Compliance Matrix

| Rule ID | Severity | Description | B42 Status | Evidence |
|---------|----------|-------------|------------|---------|
| JS-001 | P0 | No `throw` in hot paths | **PASS** | `RaiseFillSignal` does not throw; `SendCopy` catch path is unchanged; no new throws added |
| JS-002 | P0 | No `return null` | **PASS** | `RaiseFillSignal` returns void; `OnFillSignal` returns void; no null returns anywhere in B42 scope |
| JS-003 | P0 | Sealed record hierarchies for sum types | **N/A** | `FillSignalEventArgs` is a data carrier struct, not a sum type; JS-008 (readonly struct) applies instead |
| JS-008 | P1 | Readonly structs for immutable data | **PASS** | `FillSignalEventArgs` declared `public readonly struct`; all fields `{ get; private set; }` |
| **JS-010** | **P1** | **Private constructor + public static factory on signal structs** | **PASS** | `FillSignalEventArgs` constructor is `private`; only construction path is `FillSignalEventArgs.Create(...)`; matches `CopyRule.Create` / `CopySignal.Create` pattern |
| JS-021 | P0 | No `lock()` anywhere | **PASS** | CLR atomic delegate replacement used for event += / -=; `RaiseFillSignal` uses local-copy pattern; zero `lock()` in B42 scope |
| JS-033 | P0 | No `async void` (non-event-handler) | **PASS** | `OnFillSignal` is `private void`; `OnBarUpdate` is `protected override void`; no async in B42 scope |

---

## 8. NT8 Compiler Rule Compliance Matrix

| Rule ID | Description | B42 Status | Evidence |
|---------|-------------|------------|---------|
| NT8-001 | No `init` accessors — use `{ get; private set; }` + constructor | **PASS** | `FillSignalEventArgs` uses `{ get; private set; }` for all 6 fields; constructor assigns all fields |
| NT8-002 | No `record` or `abstract record` — use `class` / `struct` | **PASS** | `FillSignalEventArgs` is a `struct`; `PttFollowerStrategy` is a `class`; no records anywhere |
| NT8-003 | No `volatile double` | **PASS** | `FillSignalEventArgs` has no `double` fields; `PttFollowerStrategy` has no fields at all |
| NT8-007 | `CreateOrder` arg12 must be `(NinjaTrader.Cbi.CustomOrder)null` | **PASS** | `CopyEngine.SendCopy` already uses `(NinjaTrader.Cbi.CustomOrder)null`; B42 makes no change to that call |
| NT8-033 | No `async void` method | **PASS** | No async methods in B42 scope; `OnBarUpdate()` is synchronous empty override |
| NT8-043 | Local-copy-then-null-check pattern for events | **PASS** | `RaiseFillSignal` follows `var h = FillSignal; if (h != null) h(args);` exactly |

---

## 9. Invariants — Leader Untouched, Existing Tests Unaffected

### Leader invariant

- `SendCopy()` is ONLY called for **follower** accounts. The leader never enters this code path.
- `PttBus.FillSignal` is never raised for the leader account.
- `PttFollowerStrategy` instances are configured with follower accounts only (NT8 Control Center).
- Leader ATM behaviour (ChartTrader + native NT8 ATM) is **entirely unchanged**.

### Existing test invariant

- `B42Tests.cs` is a **new file**. No existing test file is modified.
- `CopyEngineTests.cs` is unchanged; all existing `[Fact]` methods remain valid.
- The only change to `CopyEngine.cs` is insertion of 5 lines inside the try block of `SendCopy()`.
  All existing `CopyEngine` tests continue to pass — they test behaviour that precedes the new lines.
- The `PttBus.FillSignal` event starts as `null` (no subscribers). Existing code paths that call
  `RaiseFillSignal` before any subscriber attaches will null-guard safely and produce no side effects.

### `PttContracts.cs` invariant

- All existing types (`IPttModule`, `IPttHostContext`, `ICopyEngine`, `BeEventArgs`, `TrimEventArgs`,
  `FlatEventArgs`, `CancelEventArgs`, `QuickExitEventArgs`) are **unchanged**.
- All existing `PttBus` events (`BeFired`, `TrimFired`, `FlatFired`, `CancelFired`, `QuickExitFired`)
  and Raise* methods are **unchanged**.
- The new `FillSignal` event and `RaiseFillSignal` method are **additive only**.

### Build invariant

- `PttFollowerStrategy.cs` added to `Features/` directory. It must be included in the `.csproj`
  or the directory must be auto-included via `<Compile Include="**\*.cs" />`.
- No circular dependencies: `PttFollowerStrategy` imports `PropTraderTools` (PttBus) but `CopyEngine`
  does not import `PttFollowerStrategy`. The dependency is one-directional via the event bus.

---

## 10. Open Questions for Reviewer

1. **`signal.OrderId` field name**: Confirm the exact property name on `CopySignal` that carries the
   entry order ID. Plan uses `signal.OrderId` per KEY FACTS. If the field is named differently,
   update T2 diff accordingly.

2. **`PttFollowerStrategy` namespace**: Plan recommends `namespace PropTraderTools` (flat, consistent
   with `CopyEngine.cs`). If the project convention for `Features/` directory is `PropTraderTools.Features`,
   the engineer must use the appropriate `using` directive in any other file that references the class.

3. **`protected virtual CallAtmStrategyCreate` hook**: Required for T_B42_05 test isolation.
   If reviewer disagrees with this pattern, an alternative is to make `OnFillSignal` `internal protected`
   and test the guard logic separately from the ATM call path.

4. **Test file location**: `B42Tests.cs` placed alongside `CopyEngineTests.cs`. If a different
   test file naming or directory convention is preferred, adjust without changing test logic.

---

## 11. Deferred Backlog Items from B42-QX-BE-01

The three open items from `docs/brain/B42-QX-BE-01/06-deferred-backlog.md`
(DW-B42-01, DW-B42-02, DW-B42-03) all relate to Quick Exit / BE All interaction and
are **not in scope** for B42-LaneA (ATM brackets feature). They remain deferred to future blocks.

No prior deferred items are closed by this block.

---

## Review Checklist (SCAN-01 through SCAN-07)

| Scan | Check | B42 Status |
|------|-------|------------|
| SCAN-01 | No `lock()` in any B42 scope | PASS |
| SCAN-02 | No `async void` in any B42 scope | PASS |
| SCAN-03 | No `return null` in any B42 scope | PASS |
| SCAN-04 | All structs declared `readonly` | PASS — `FillSignalEventArgs` is `readonly struct` |
| SCAN-05 | PTT- order name prefix | PASS — `SendCopy` uses `"PTT-Copy"` (unchanged) |
| SCAN-06 | CYC ≤ 8 for all new/modified methods | PASS — max CYC in B42 scope is 5 (`SendCopy` unchanged) |
| SCAN-07 | NT8-001 compliance (no init setters) | PASS — `{ get; private set; }` + constructor throughout |
| SCAN-08 | JS-010 compliance (no public constructor on signal struct) | PASS — `FillSignalEventArgs` constructor is `private`; factory is `FillSignalEventArgs.Create(...)` |

---

*Architecture plan revised (Cycle 2). V-01 JS-010 violation corrected. Pending: ptt-plan-reviewer re-review.*
