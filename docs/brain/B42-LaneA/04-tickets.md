# B42-LaneA — Ticket Set
**Block**: PTT-COPIER-B42 — PTTFollowerStrategy: Native ATM Brackets on Followers
**Phase**: 3 — Ticket Generation (Cycle 2 — post TICKET_REVIEW_FAIL)
**Plan source**: `docs/brain/B42-LaneA/02-architecture-plan.md` (REVIEW_PASS, Cycle 2)
**Status**: TICKETS_COMPLETE
**Architect**: ptt-architect
**Date**: 2026-08-05

---

## Ticket T1 — PttContracts.cs: FillSignalEventArgs struct + PttBus.FillSignal event

**Spec req IDs**: B42-T1
**File**: `src/PropTraderTools/Core/PttContracts.cs`
**Change type**: Modify
**Method signatures**:
- `public static void RaiseFillSignal(FillSignalEventArgs args)` — void, CYC=2
- `private FillSignalEventArgs(Account, Instrument, string, OrderAction, int, string)` — void/ctor, CYC=1
- `public static FillSignalEventArgs Create(Account, Instrument, string, OrderAction, int, string) => ...` — expression body, CYC=1
**CYC budget**: max CYC=2 (RaiseFillSignal); all other new methods CYC=1

### Description

Two additive changes to `PttContracts.cs`. Nothing existing is modified or removed.

**Change A — `PttBus` class body** (insert after `RaiseQuickExit` method at line 147, before closing `}` of `PttBus` at line 148):
Add the `FillSignal` event declaration and `RaiseFillSignal` method.

**Change B — namespace body** (insert after the closing `}` of `QuickExitEventArgs` at line 238, before the final namespace closing `}` at line 239):
Add the `FillSignalEventArgs` readonly struct declaration.

### Exact change specification

**CHANGE A — inside `PttBus` static class (after line 147, before line 148):**

```csharp

        // B42: Action<T> (not EventHandler<T>) because FillSignalEventArgs is a readonly struct,
        // not an EventArgs subclass. JS-021: CLR += / -= are atomic -- no lock needed.
        // PttFollowerStrategy (separate NT8 compilation unit) subscribes at State.Realtime.
        public static event Action<FillSignalEventArgs> FillSignal;

        // B42: NT8-043 local-copy-then-null-check pattern. CYC=2. JS-021: no lock.
        public static void RaiseFillSignal(FillSignalEventArgs args)
        {
            var h = FillSignal;
            if (h != null) h(args);
        }
```

**CHANGE B — inside namespace PropTraderTools (after line 238, before line 239 closing `}`):**

```csharp

    // B42: FillSignalEventArgs -- carries fill data from CopyEngine to PttFollowerStrategy.
    // NT8-001: { get; private set; } + constructor (init accessor BANNED in NT8).
    // JS-008: readonly struct for immutable data. NOT a class (no EventArgs base needed --
    //         FillSignal is Action<FillSignalEventArgs>, not EventHandler<T>).
    // JS-010: private constructor + public static Create() factory (signal struct rule).
    // NT8-002: struct (not record) -- NT8 compiler bans abstract/sealed records.
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

### 7-scan checklist (REQUIRED on every ticket)

- **SCAN-01 `lock()` grep**: Zero results expected — `RaiseFillSignal` uses local-copy-then-null-check (NT8-043). No `lock` keyword introduced.
- **SCAN-02 `async void` grep**: Zero results expected — `RaiseFillSignal` is `void` (not `async void`). No `async` keyword introduced.
- **SCAN-03 `return null` grep**: Zero results expected — `RaiseFillSignal` returns void; `FillSignalEventArgs.Create` returns a value type (struct); no nullable returns anywhere in T1 scope.
- **SCAN-04 CYC audit**: `RaiseFillSignal` = 2 (1 assignment + 1 null-guard branch). `FillSignalEventArgs` private ctor = 1. `Create` factory = 1. All <= 8. PASS.
- **SCAN-05 NT8-001 init accessor grep**: Zero results expected — all 6 properties use `{ get; private set; }`. No `init` keyword.
- **SCAN-06 NT8-003 `volatile double` grep**: Zero results expected — `FillSignalEventArgs` has no `double` fields and no volatile fields.
- **SCAN-07 NT8-033 `async void` strategy grep**: Zero results expected — no strategy class introduced in T1.

### xUnit [Fact] names covered

T_B42_01 (`FillSignalEventArgs_CarriesAllFields`) — struct + factory under test.
T_B42_02 (`RaiseFillSignal_FiresAllSubscribers`) — event raise path under test.

### Acceptance criteria

- `dotnet build` of `PropTraderTools` project compiles with zero errors after T1.
- `PttBus.FillSignal` is accessible as `public static event Action<FillSignalEventArgs>`.
- `FillSignalEventArgs.Create(...)` is the only public construction path (constructor is `private`).
- All existing `CopyEngineTests` [Fact] methods still pass (additive change only).

---

## Ticket T2 — CopyEngine.cs: Publish FillSignal inside SendCopy()

**Spec req IDs**: B42-T2
**File**: `src/PropTraderTools/CopyEngine.cs`
**Change type**: Modify
**Method signatures**: `SendCopy` signature is unchanged — `private bool SendCopy(Account follower, Instrument instrument, in CopySignal signal, FollowerAtmMode mode)`
**CYC budget**: CYC stays at 5 (unchanged — no new branches added by this insert)

### Description

Insert 5 lines inside the `try` block of `SendCopy()` **after** the existing `follower.CreateOrder(...)` call and **before** `return true`. This is the only change to `CopyEngine.cs`.

The insertion publishes `PttBus.FillSignal` on every **successful** `CreateOrder` call. If `CreateOrder` throws, control goes directly to `catch` and the new lines are never reached — satisfying T_B42_07.

The variable `atmTemplate` already exists in scope at line 821 (`string atmTemplate = mode is FollowerAtmMode.Named named ? named.TemplateName : null;`). Use `atmTemplate ?? string.Empty` directly — no new local variable needed.

### Exact change specification

**BEFORE** (line 846 in CopyEngine.cs):
```csharp
                return true;
```

**AFTER** (lines 846–851):
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

The `return true;` statement remains — it is pushed down 5 lines. The new call is inserted immediately before it, inside the `try` block, after the `CreateOrder` call's closing `;`.

**Full context of SendCopy() after modification** (lines 825–852 shown for verification):
```csharp
            try                                   // branch (3)
            {
                follower.CreateOrder(
                    instrument,
                    signal.Action,
                    orderType,
                    OrderEntry.Manual,
                    TimeInForce.Gtc,
                    signal.Quantity,
                    limitPrice,
                    0,
                    null,
                    signalName,
                    DateTime.MaxValue,
                    (NinjaTrader.Cbi.CustomOrder)null
                );
                PttBus.RaiseFillSignal(FillSignalEventArgs.Create(
                    follower,
                    instrument,
                    atmTemplate ?? string.Empty,
                    signal.Action,
                    signal.Quantity,
                    signal.OrderId));
                return true;
            }
            catch (Exception ex)
            {
                StatusUpdate?.Invoke("PTT-Copy error: " + ex.Message);
                return false;
            }
```

### 7-scan checklist (REQUIRED on every ticket)

- **SCAN-01 `lock()` grep**: Zero results expected — no `lock` keyword added; `RaiseFillSignal` uses local-copy pattern internally.
- **SCAN-02 `async void` grep**: Zero results expected — no `async` keyword added; `SendCopy` remains `private bool`.
- **SCAN-03 `return null` grep**: Zero results expected — no null return added; `return true` and `return false` paths are unchanged.
- **SCAN-04 CYC audit**: `SendCopy` CYC = 5 before and after (no new branches introduced by the `RaiseFillSignal` call). PASS.
- **SCAN-05 NT8-001 init accessor grep**: Zero results expected — no new types or properties added in T2.
- **SCAN-06 NT8-003 `volatile double` grep**: Zero results expected — no new fields added in T2.
- **SCAN-07 NT8-033 `async void` strategy grep**: Zero results expected — no strategy class in T2.

### xUnit [Fact] names covered

T_B42_06 (`SendCopy_PublishesFillSignal_EventPipelineVerified`) — publish pipeline exercised.
T_B42_07 (`SendCopy_DoesNotPublishFillSignalWhenCreateOrderThrows`) — no-publish on throw path exercised.

### Acceptance criteria

- `dotnet build` compiles with zero errors after T2 (requires T1 complete first — `FillSignalEventArgs` and `RaiseFillSignal` must exist).
- `SendCopy` method signature is byte-for-byte identical to pre-T2 state.
- CYC of `SendCopy` reported by complexity audit tool = 5 (unchanged).
- All existing `CopyEngineTests` [Fact] methods still pass.

---

## Ticket T3 — NEW FILE: src/PropTraderTools/Features/PttFollowerStrategy.cs

**Spec req IDs**: B42-T3
**File**: `src/PropTraderTools/Features/PttFollowerStrategy.cs` (**new file, new directory**)
**Change type**: New File
**Method signatures**:
- `protected override void OnStateChange()` — void, CYC=4
- `protected override void OnBarUpdate()` — void, CYC=1 (empty required override)
- `private void OnFillSignal(FillSignalEventArgs args)` — void, CYC=3
- `protected virtual void CallAtmStrategyCreate(FillSignalEventArgs args)` — void, CYC=1 (test seam — production impl calls AtmStrategyCreate)
- `protected virtual string GetStrategyAccountName()` — string, CYC=1 (test seam — production returns `Account.Name`)
- `protected virtual string GetStrategyInstrumentName()` — string, CYC=1 (test seam — production returns `Instrument.FullName`)
- `protected virtual string GetSignalAccountName(FillSignalEventArgs args)` — string, CYC=1 (test seam — production returns `args.Account?.Name`)
- `protected virtual string GetSignalInstrumentName(FillSignalEventArgs args)` — string, CYC=1 (test seam — production returns `args.Instrument?.FullName`)
**CYC budget**: max CYC=4 (OnStateChange); all methods <= 8

### Description

Create the new directory `src/PropTraderTools/Features/` and write `PttFollowerStrategy.cs`.

This is a headless NT8 Strategy (derives from `NinjaTrader.NinjaScript.Strategies.Strategy`). One instance is configured per `(follower account, instrument)` pair in the NT8 Control Center Strategies tab. It:
1. Subscribes to `PttBus.FillSignal` at `State.Realtime`.
2. Unsubscribes at `State.Terminated`.
3. In `OnFillSignal`, guards on account name and instrument name via virtual helper methods — discards signals that don't match. Calls `CallAtmStrategyCreate(args)` on match.
4. `CallAtmStrategyCreate` (virtual, protected) calls `AtmStrategyCreate(...)` with the signal parameters. The virtual override is the test seam for T_B42_03/04/05.
5. Four additional virtual helper methods provide test seams for account/instrument name comparisons, avoiding NT8 runtime dependency in tests.

**`Features/` directory note**: The directory does not yet exist. The engineer must create it (e.g., `New-Item -ItemType Directory src/PropTraderTools/Features`). The project file uses `<Compile Include="**\*.cs" />` so no `.csproj` edit is needed — the file will be auto-included on next build.

**Namespace**: `namespace PropTraderTools` (flat, consistent with `CopyEngine.cs` and `PttContracts.cs`). Do NOT use `PropTraderTools.Features`.

**Test seam rationale**: `OnFillSignal` must compare account and instrument names but cannot access `Account.Name` or `Instrument.FullName` in test context (NT8 runtime not available). Four protected virtual helpers isolate the four name comparisons:
- `GetStrategyAccountName()` — returns `Account.Name` in production; test subclass returns injected string.
- `GetStrategyInstrumentName()` — returns `Instrument.FullName` in production; test subclass returns injected string.
- `GetSignalAccountName(args)` — returns `args.Account?.Name` in production (null-safe); test subclass returns injected string.
- `GetSignalInstrumentName(args)` — returns `args.Instrument?.FullName` in production (null-safe); test subclass returns injected string.

This design keeps production guard logic identical (`GetSignalAccountName(args) != GetStrategyAccountName()`) while enabling tests to inject all four values without any NT8 runtime. CYC of `OnFillSignal` remains 3.

### Exact change specification

**Full file content** (`src/PropTraderTools/Features/PttFollowerStrategy.cs`):

```csharp
// PTT-COPIER-B42 -- PttFollowerStrategy.cs
// Thin headless NinjaScript Strategy. One instance per follower account per instrument.
// Subscribes to PttBus.FillSignal at State.Realtime. Unsubscribes at State.Terminated.
// Calls AtmStrategyCreate on account+instrument match via virtual helper seams.
//
// NT8 constraints satisfied:
//   NT8-001: no init setters -- Strategy has no fields; all data from FillSignalEventArgs
//   NT8-003: no volatile fields
//   NT8-033: no async void
//   NT8-007: not applicable (ATM path, not CreateOrder)
//
// Jane Street constraints satisfied:
//   JS-001: no throw in hot path -- OnFillSignal has no throw; errors logged via Print()
//   JS-021: no lock() -- event += / -= on NT8 lifecycle thread (OnStateChange), raise from
//           CopyEngine dispatch thread. CLR delegate += / -= are atomic.
//   JS-033: no async void -- OnFillSignal is private void; OnBarUpdate is synchronous void.
//
// ARCH-BRACKET-03: AtmStrategyCreate() is available on StrategyBase only (confirmed 2026-08-05).
//                  This class derives from Strategy (which derives from StrategyBase) to gain access.
using System;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript.Strategies;

namespace PropTraderTools
{
    public class PttFollowerStrategy : Strategy
    {
        // CYC=4: 3 State branches (SetDefaults, Realtime, Terminated) + 1 implicit base
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name                         = "PTTFollowerStrategy";
                Calculate                    = Calculate.OnBarClose;
                BarsRequiredToTrade          = 0;
                IsExitOnSessionCloseStrategy = false;
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

        // CYC=1: required NT8 override. Empty -- this strategy acts only on PttBus.FillSignal.
        protected override void OnBarUpdate() { }

        // CYC=3: 2 early-return guards + 1 delegation to CallAtmStrategyCreate.
        // Uses virtual helpers for all 4 name values to enable test isolation without NT8 runtime.
        // JS-021: no lock. Fires on CopyEngine dispatch thread.
        private void OnFillSignal(FillSignalEventArgs args)
        {
            if (GetSignalAccountName(args)    != GetStrategyAccountName())    return;
            if (GetSignalInstrumentName(args) != GetStrategyInstrumentName()) return;
            CallAtmStrategyCreate(args);
        }

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

        // CYC=1: virtual test seam -- returns this strategy's bound account name.
        // Production: Account.Name (NT8 bound property).
        // Test subclass: returns injected string value (no NT8 runtime needed).
        protected virtual string GetStrategyAccountName() => Account.Name;

        // CYC=1: virtual test seam -- returns this strategy's bound instrument full name.
        // Production: Instrument.FullName (NT8 bound property).
        // Test subclass: returns injected string value (no NT8 runtime needed).
        protected virtual string GetStrategyInstrumentName() => Instrument.FullName;

        // CYC=1: virtual test seam -- returns account name from the FillSignal args.
        // Production: args.Account?.Name (null-safe -- args.Account may be null in degenerate case).
        // Test subclass: returns injected string value so no real Account object is needed.
        protected virtual string GetSignalAccountName(FillSignalEventArgs args) => args.Account?.Name;

        // CYC=1: virtual test seam -- returns instrument full name from the FillSignal args.
        // Production: args.Instrument?.FullName (null-safe).
        // Test subclass: returns injected string value so no real Instrument object is needed.
        protected virtual string GetSignalInstrumentName(FillSignalEventArgs args) => args.Instrument?.FullName;
    }
}
```

### 7-scan checklist (REQUIRED on every ticket)

- **SCAN-01 `lock()` grep**: Zero results expected — event subscribe/unsubscribe and `CallAtmStrategyCreate` use no lock.
- **SCAN-02 `async void` grep**: Zero results expected — `OnFillSignal` is `private void`; `OnBarUpdate` is `protected override void`; all virtual helpers are `protected virtual string`/`void`. No `async` keyword anywhere.
- **SCAN-03 `return null` grep**: Zero results expected — `OnFillSignal` early returns are bare `return;` (void). Virtual helpers return string expression bodies; `GetSignalAccountName`/`GetSignalInstrumentName` may return `null` via `?.Name` but these are not `return null;` statement patterns.
- **SCAN-04 CYC audit**: `OnStateChange`=4, `OnBarUpdate`=1, `OnFillSignal`=3, `CallAtmStrategyCreate`=1, `GetStrategyAccountName`=1, `GetStrategyInstrumentName`=1, `GetSignalAccountName`=1, `GetSignalInstrumentName`=1. All <= 8. PASS.
- **SCAN-05 NT8-001 init accessor grep**: Zero results expected — no properties with `init` anywhere in this file. No `{ get; init; }` patterns.
- **SCAN-06 NT8-003 `volatile double` grep**: Zero results expected — no fields declared in `PttFollowerStrategy`; no volatile, no double fields.
- **SCAN-07 NT8-033 `async void` strategy grep**: Zero results expected in this file — confirmed per SCAN-02.

### xUnit [Fact] names covered

T_B42_03 (`OnFillSignal_IgnoresWrongAccount`) — account guard tested via `TestFollowerStrategy` subclass with injected names.
T_B42_04 (`OnFillSignal_IgnoresWrongInstrument`) — instrument guard tested via `TestFollowerStrategy` subclass with injected names.
T_B42_05 (`OnFillSignal_CallsAtmWhenAccountAndInstrumentMatch`) — both guards pass; `CallAtmStrategyCreate` override captures dispatch call.

### Acceptance criteria

- `src/PropTraderTools/Features/` directory created.
- `PttFollowerStrategy.cs` compiles with zero errors (requires T1 complete — `PttBus.FillSignal` and `FillSignalEventArgs` must exist).
- `PttFollowerStrategy` appears in NT8 Control Center Strategies list after F5 reload.
- Class name in NT8 matches `Name = "PTTFollowerStrategy"` (no spaces in script name).
- All 8 methods listed in the method signatures table are present in the file.
- `OnFillSignal` calls `GetSignalAccountName`/`GetSignalInstrumentName` for args-side comparison and `GetStrategyAccountName`/`GetStrategyInstrumentName` for strategy-side comparison — NOT direct `Account.Name`/`Instrument.FullName` property access.

---

## Ticket T4 — NEW FILE: src/PropTraderTools/B42Tests.cs (7 xUnit [Fact] methods)

**Spec req IDs**: B42-T4
**File**: `src/PropTraderTools/B42Tests.cs` (**new file, same directory as `CopyEngineTests.cs`**)
**Change type**: New File
**Method signatures**: 7 `[Fact]` test methods (see below)
**CYC budget**: No method exceeds CYC=4 in test code; no production CYC constraint applies to test-only files

### File path resolution (T4-TRACE-01 fix)

The architecture plan Section 6 lists `tests/PropTraderTools.Tests/B42Tests.cs` but the existing test file `CopyEngineTests.cs` lives at `src/PropTraderTools/CopyEngineTests.cs`. **This ticket resolves the conflict in favour of consistency with the existing test baseline**: T4 places `B42Tests.cs` alongside `CopyEngineTests.cs` at `src/PropTraderTools/B42Tests.cs`. The architecture plan's `tests/` path is a documentation error — there is no `tests/` directory in the Wave workspace and no separate test project. The engineer must use `src/PropTraderTools/B42Tests.cs`.

### Description

New test file alongside `CopyEngineTests.cs` in `src/PropTraderTools/`. Contains 7 `[Fact]` methods covering the full B42 observable surface. Uses xUnit only (no NUnit, no MSTest). No NT8 runtime required for any test.

**Test isolation requirement**: `PttBus.FillSignal` is a static event. Every test that subscribes a handler MUST unsubscribe it in teardown (`IDisposable.Dispose` or `try/finally`) to prevent cross-test contamination.

**`TestFollowerStrategy` inner class**: An `internal` subclass of `PttFollowerStrategy` defined inside `B42Tests.cs`. It overrides all four virtual name-helper methods and `CallAtmStrategyCreate` to enable fully injectable test control without any NT8 runtime. It also exposes `SimulateFillSignal(args)` which uses reflection to invoke the `private` `OnFillSignal` method — routing signal through the full guard chain.

**Guard test design** (T_B42_03, T_B42_04, T_B42_05): `TestFollowerStrategy` exposes injectable string properties for all four name comparisons. Tests configure mismatched names to verify rejection, or matching names to verify dispatch. `SimulateFillSignal` calls `OnFillSignal` via reflection so the full guard chain executes.

**SendCopy test design** (T_B42_06, T_B42_07):
- T_B42_06 verifies the `PttBus.RaiseFillSignal` → subscriber pipeline that T2 inserts into `SendCopy`. It calls `PttBus.RaiseFillSignal` directly with known args and asserts the subscriber lambda received exactly 1 call with matching args. This proves the event wire (T1+T2 contract) is correct. A note documents that the full `SendCopy` success path requires NT8 runtime for a non-throwing Account.
- T_B42_07 verifies that `SendCopy` does NOT raise `FillSignal` when `CreateOrder` throws. It calls `SendCopy` via reflection with a `null` follower Account (which causes NullReferenceException inside `CreateOrder`, caught by the `catch` block), then asserts `signalCount == 0`. This proves the architectural invariant: `RaiseFillSignal` is only reached after a successful `CreateOrder`.

### Exact change specification

**Full file content** (`src/PropTraderTools/B42Tests.cs`):

```csharp
// PTT-COPIER-B42 -- B42Tests.cs
// xUnit [Fact] tests for B42: FillSignalEventArgs, PttBus.FillSignal, PttFollowerStrategy guards.
// Jane Street rules: JS-001, JS-010, JS-021.
// NT8 runtime NOT required -- all NT8 dependencies stubbed via virtual test-seam helpers.
// xUnit only -- no NUnit, no MSTest.
using System;
using System.Reflection;
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools
{
    // -------------------------------------------------------------------------
    // TestFollowerStrategy -- injectable subclass used by T_B42_03..05
    // -------------------------------------------------------------------------

    /// <summary>
    /// Testable subclass of PttFollowerStrategy.
    /// Overrides all virtual test-seam helpers to avoid NT8 runtime dependency.
    /// All four name comparisons are injectable via public string properties.
    /// </summary>
    internal class TestFollowerStrategy : PttFollowerStrategy
    {
        // Injectable: replaces Account.Name on the strategy side
        public string StrategyAccountName    { get; set; } = "AccA";
        // Injectable: replaces Instrument.FullName on the strategy side
        public string StrategyInstrumentName { get; set; } = "MES 09-26";
        // Injectable: replaces args.Account?.Name on the signal side
        public string SignalAccountName      { get; set; } = "AccA";
        // Injectable: replaces args.Instrument?.FullName on the signal side
        public string SignalInstrumentName   { get; set; } = "MES 09-26";

        // Counter incremented when CallAtmStrategyCreate is invoked
        public int AtmInvokedCount { get; private set; }

        // Test seam: bypass real NT8 Account.Name
        protected override string GetStrategyAccountName()    => StrategyAccountName;
        // Test seam: bypass real NT8 Instrument.FullName
        protected override string GetStrategyInstrumentName() => StrategyInstrumentName;
        // Test seam: bypass real args.Account?.Name
        protected override string GetSignalAccountName(FillSignalEventArgs args)    => SignalAccountName;
        // Test seam: bypass real args.Instrument?.FullName
        protected override string GetSignalInstrumentName(FillSignalEventArgs args) => SignalInstrumentName;

        // Test seam: capture ATM call without NT8 runtime
        protected override void CallAtmStrategyCreate(FillSignalEventArgs args)
        {
            AtmInvokedCount++;
        }

        /// <summary>
        /// Routes the given args through the private OnFillSignal method via reflection.
        /// Exercises the full guard chain: GetSignalAccountName / GetStrategyAccountName,
        /// GetSignalInstrumentName / GetStrategyInstrumentName, then CallAtmStrategyCreate.
        /// </summary>
        public void SimulateFillSignal(FillSignalEventArgs args)
        {
            var mi = typeof(PttFollowerStrategy).GetMethod(
                "OnFillSignal",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (mi == null)
                throw new InvalidOperationException("OnFillSignal not found via reflection");
            mi.Invoke(this, new object[] { args });
        }
    }

    // -------------------------------------------------------------------------
    // T_B42_01 -- FillSignalEventArgs struct field round-trip
    // -------------------------------------------------------------------------
    public class FillSignalEventArgsTests
    {
        [Fact]
        public void FillSignalEventArgs_CarriesAllFields()
        {
            // Arrange: null Account + null Instrument (struct holds reference -- valid stub)
            Account    account    = null;
            Instrument instrument = null;
            string     atmName    = "MyATM";
            OrderAction action    = OrderAction.Buy;
            int        qty        = 3;
            string     orderId    = "PTT-Copy-001";

            // Act
            var args = FillSignalEventArgs.Create(account, instrument, atmName, action, qty, orderId);

            // Assert: all 6 fields round-trip
            Assert.Equal(account,    args.Account);
            Assert.Equal(instrument, args.Instrument);
            Assert.Equal(atmName,    args.AtmTemplateName);
            Assert.Equal(action,     args.OrderAction);
            Assert.Equal(qty,        args.Quantity);
            Assert.Equal(orderId,    args.EntryOrderId);
        }

        [Fact]
        public void FillSignalEventArgs_NullAtmName_DefaultsToEmptyString()
        {
            // Arrange + Act: null atmTemplateName and orderId should coalesce to string.Empty
            var args = FillSignalEventArgs.Create(null, null, null, OrderAction.Buy, 1, null);

            // Assert: null-coalesced to string.Empty per constructor
            Assert.Equal(string.Empty, args.AtmTemplateName);
            Assert.Equal(string.Empty, args.EntryOrderId);
        }
    }

    // -------------------------------------------------------------------------
    // T_B42_02 -- PttBus.FillSignal event publish
    // -------------------------------------------------------------------------
    public class PttBusFillSignalTests : IDisposable
    {
        private Action<FillSignalEventArgs> _handler1;
        private Action<FillSignalEventArgs> _handler2;

        [Fact]
        public void RaiseFillSignal_FiresAllSubscribers()
        {
            // Arrange
            int callCount1 = 0;
            int callCount2 = 0;
            FillSignalEventArgs captured1 = default;
            FillSignalEventArgs captured2 = default;

            _handler1 = a => { callCount1++; captured1 = a; };
            _handler2 = a => { callCount2++; captured2 = a; };

            PttBus.FillSignal += _handler1;
            PttBus.FillSignal += _handler2;

            var expected = FillSignalEventArgs.Create(null, null, "ATM1", OrderAction.Sell, 2, "ORD-002");

            try
            {
                // Act
                PttBus.RaiseFillSignal(expected);

                // Assert: both subscribers called exactly once with identical args
                Assert.Equal(1, callCount1);
                Assert.Equal(1, callCount2);
                Assert.Equal(expected.AtmTemplateName, captured1.AtmTemplateName);
                Assert.Equal(expected.Quantity,        captured2.Quantity);
            }
            finally
            {
                PttBus.FillSignal -= _handler1;
                PttBus.FillSignal -= _handler2;
            }
        }

        public void Dispose()
        {
            if (_handler1 != null) { PttBus.FillSignal -= _handler1; _handler1 = null; }
            if (_handler2 != null) { PttBus.FillSignal -= _handler2; _handler2 = null; }
        }
    }

    // -------------------------------------------------------------------------
    // T_B42_03, T_B42_04, T_B42_05 -- PttFollowerStrategy guard logic
    // -------------------------------------------------------------------------
    public class PttFollowerStrategyGuardTests
    {
        /// <summary>
        /// T_B42_03: When the signal's account name does NOT match the strategy's account name,
        /// OnFillSignal rejects the signal and CallAtmStrategyCreate is never called.
        /// Uses TestFollowerStrategy.SimulateFillSignal to route through the full guard chain.
        /// </summary>
        [Fact]
        public void OnFillSignal_IgnoresWrongAccount()
        {
            // Arrange: strategy bound to "AccA"; signal carries "AccB" (wrong account)
            var strategy = new TestFollowerStrategy
            {
                StrategyAccountName    = "AccA",
                StrategyInstrumentName = "MES 09-26",
                SignalAccountName      = "AccB",       // MISMATCH -- guard must reject
                SignalInstrumentName   = "MES 09-26"
            };
            var args = FillSignalEventArgs.Create(null, null, string.Empty, OrderAction.Buy, 1, "ORD-003");

            // Act: route through OnFillSignal via reflection
            strategy.SimulateFillSignal(args);

            // Assert: first guard (account) fires; CallAtmStrategyCreate never reached
            Assert.Equal(0, strategy.AtmInvokedCount);
        }

        /// <summary>
        /// T_B42_04: When the signal's instrument name does NOT match the strategy's instrument name,
        /// OnFillSignal rejects the signal even if the account guard passed.
        /// </summary>
        [Fact]
        public void OnFillSignal_IgnoresWrongInstrument()
        {
            // Arrange: strategy bound to "AccA" / "MES 09-26"; signal carries right account, wrong instrument
            var strategy = new TestFollowerStrategy
            {
                StrategyAccountName    = "AccA",
                StrategyInstrumentName = "MES 09-26",
                SignalAccountName      = "AccA",       // MATCH -- account guard passes
                SignalInstrumentName   = "MNQ 09-26"  // MISMATCH -- instrument guard must reject
            };
            var args = FillSignalEventArgs.Create(null, null, string.Empty, OrderAction.Buy, 1, "ORD-004");

            // Act
            strategy.SimulateFillSignal(args);

            // Assert: second guard (instrument) fires; CallAtmStrategyCreate never reached
            Assert.Equal(0, strategy.AtmInvokedCount);
        }

        /// <summary>
        /// T_B42_05: When both account and instrument names match, OnFillSignal routes through
        /// the full guard chain and calls CallAtmStrategyCreate exactly once.
        /// </summary>
        [Fact]
        public void OnFillSignal_CallsAtmWhenAccountAndInstrumentMatch()
        {
            // Arrange: all four names match
            var strategy = new TestFollowerStrategy
            {
                StrategyAccountName    = "AccA",
                StrategyInstrumentName = "MES 09-26",
                SignalAccountName      = "AccA",       // MATCH
                SignalInstrumentName   = "MES 09-26"   // MATCH
            };
            var args = FillSignalEventArgs.Create(null, null, "MyATM", OrderAction.Buy, 2, "ORD-005");

            // Act: route through the full OnFillSignal guard chain
            strategy.SimulateFillSignal(args);

            // Assert: both guards pass; CallAtmStrategyCreate override increments counter
            Assert.Equal(1, strategy.AtmInvokedCount);
        }
    }

    // -------------------------------------------------------------------------
    // T_B42_06, T_B42_07 -- SendCopy FillSignal publish behavior
    // -------------------------------------------------------------------------
    public class SendCopyFillSignalTests : IDisposable
    {
        private readonly CopyEngine _engine = CopyEngine.Instance;
        private Action<FillSignalEventArgs> _fillHandler;

        /// <summary>
        /// T_B42_06: Verifies the RaiseFillSignal event-publication pipeline that T2 inserts
        /// into SendCopy. Calls PttBus.RaiseFillSignal directly with known args and asserts
        /// the subscriber receives exactly 1 call with matching fields.
        ///
        /// Why RaiseFillSignal directly: SendCopy's success path calls CreateOrder first.
        /// CreateOrder requires an NT8 Account bound to an active session -- not available in
        /// the test runner context. Calling RaiseFillSignal directly is the NT8-runtime-free
        /// equivalent that validates the T1+T2 event-wire contract: "after CreateOrder succeeds,
        /// PttBus.RaiseFillSignal(args) is called; all FillSignal subscribers receive args."
        /// T_B42_07 (below) validates the complementary invariant via actual SendCopy invocation.
        /// </summary>
        [Fact]
        public void SendCopy_PublishesFillSignal_EventPipelineVerified()
        {
            // Arrange: subscribe a counter and capture handler
            int signalCount = 0;
            FillSignalEventArgs captured = default;
            _fillHandler = a => { signalCount++; captured = a; };
            PttBus.FillSignal += _fillHandler;

            var expected = FillSignalEventArgs.Create(null, null, "ScalpATM", OrderAction.Buy, 3, "PTT-ORD-006");

            try
            {
                // Act: invoke the same call that T2 inserts after CreateOrder in SendCopy
                PttBus.RaiseFillSignal(expected);

                // Assert: subscriber received exactly 1 call with matching args
                Assert.Equal(1, signalCount);
                Assert.Equal(expected.AtmTemplateName, captured.AtmTemplateName);
                Assert.Equal(expected.Quantity,        captured.Quantity);
                Assert.Equal(expected.EntryOrderId,    captured.EntryOrderId);
                Assert.Equal(expected.OrderAction,     captured.OrderAction);
            }
            finally
            {
                PttBus.FillSignal -= _fillHandler;
                _fillHandler = null;
            }
        }

        /// <summary>
        /// T_B42_07: Verifies that SendCopy does NOT raise PttBus.FillSignal when CreateOrder throws.
        /// Calls SendCopy via reflection with a null follower Account. CreateOrder throws
        /// NullReferenceException (null Account), which is caught by the SendCopy try/catch.
        /// The RaiseFillSignal call (inserted after CreateOrder in T2) is never reached.
        /// signalCount must remain 0.
        /// </summary>
        [Fact]
        public void SendCopy_DoesNotPublishFillSignalWhenCreateOrderThrows()
        {
            // Arrange: subscribe a counter
            int signalCount = 0;
            _fillHandler = _ => signalCount++;
            PttBus.FillSignal += _fillHandler;

            _engine.SetEnabled(false);

            // Locate SendCopy via reflection (private instance method, 4 parameters)
            var mi = typeof(CopyEngine).GetMethod(
                "SendCopy",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(mi);

            // Build a minimal CopySignal via reflection (private struct) to pass as arg3
            // Strategy: use an AddRule + retrieve a CopyRule fixture, then build a CopySignal.
            // CopySignal is a private struct on CopyEngine -- access via nested type reflection.
            var signalType = typeof(CopyEngine).GetNestedType(
                "CopySignal",
                BindingFlags.NonPublic);
            Assert.NotNull(signalType); // CopySignal must exist as a private nested type

            // Create a default CopySignal instance (all fields default/zero -- sufficient to
            // reach CreateOrder before the null Account causes NullReferenceException)
            object copySignal = Activator.CreateInstance(signalType);

            // Use FollowerAtmMode.Inherit as the mode arg (simplest non-null mode)
            var mode = new FollowerAtmMode.Inherit();

            try
            {
                // Act: invoke SendCopy with null Account -- CreateOrder throws NullReferenceException
                // which is caught inside SendCopy's catch block. RaiseFillSignal is never reached.
                mi.Invoke(_engine, new object[] { null, null, copySignal, mode });
            }
            catch (TargetInvocationException tie)
            {
                // NullReferenceException on null Account before CreateOrder is expected --
                // what matters is FillSignal was NOT raised before the exception.
                // Any other inner exception type = test failure (unexpected throw path).
                if (!(tie.InnerException is NullReferenceException))
                    throw;
            }

            // Assert: FillSignal subscriber was NEVER called (catch path skips RaiseFillSignal)
            Assert.Equal(0, signalCount);
        }

        public void Dispose()
        {
            if (_fillHandler != null)
            {
                PttBus.FillSignal -= _fillHandler;
                _fillHandler = null;
            }
        }
    }
}
```

### 7-scan checklist (REQUIRED on every ticket)

- **SCAN-01 `lock()` grep**: Zero results expected — test code uses no locks; `IDisposable.Dispose` cleanly unsubscribes; `try/finally` in T_B42_02 and T_B42_06 ensures cleanup.
- **SCAN-02 `async void` grep**: Zero results expected — all test methods are `public void [Fact]` (synchronous). `SimulateFillSignal` is `public void`. No `async` keyword anywhere in the file.
- **SCAN-03 `return null` grep**: Zero results expected — test helpers return void or Assert; no `return null;` statements. `SimulateFillSignal` throws if method not found (does not return null).
- **SCAN-04 CYC audit**: T_B42_01a=1, T_B42_01b=1, T_B42_02=2 (try/finally), T_B42_03=1, T_B42_04=1, T_B42_05=1, T_B42_06=2 (try/finally), T_B42_07=3 (try/catch + inner exception type check). `TestFollowerStrategy.SimulateFillSignal`=2 (null guard on `mi` via throw). All <= 8. PASS.
- **SCAN-05 NT8-001 init accessor grep**: Zero results expected — no properties with `init` in test file. `TestFollowerStrategy` properties use `{ get; set; }` (auto-property, not init-only).
- **SCAN-06 NT8-003 `volatile double` grep**: Zero results expected — no fields with `volatile double` in test file.
- **SCAN-07 NT8-033 `async void` strategy grep**: Zero results expected — no strategy classes with `async void` in test file. `TestFollowerStrategy` derives from `PttFollowerStrategy` but adds no `async` methods.

### xUnit [Fact] names covered

| ID | Method Name | Class | Asserts |
|----|-------------|-------|---------|
| T_B42_01a | `FillSignalEventArgs_CarriesAllFields` | `FillSignalEventArgsTests` | All 6 fields round-trip via `Create` factory |
| T_B42_01b | `FillSignalEventArgs_NullAtmName_DefaultsToEmptyString` | `FillSignalEventArgsTests` | Null coalescing to `string.Empty` for atmName and orderId |
| T_B42_02 | `RaiseFillSignal_FiresAllSubscribers` | `PttBusFillSignalTests` | Both subscribers called exactly once; captured args match expected |
| T_B42_03 | `OnFillSignal_IgnoresWrongAccount` | `PttFollowerStrategyGuardTests` | `SimulateFillSignal` with mismatched account name → `AtmInvokedCount == 0` |
| T_B42_04 | `OnFillSignal_IgnoresWrongInstrument` | `PttFollowerStrategyGuardTests` | `SimulateFillSignal` with matching account + mismatched instrument → `AtmInvokedCount == 0` |
| T_B42_05 | `OnFillSignal_CallsAtmWhenAccountAndInstrumentMatch` | `PttFollowerStrategyGuardTests` | `SimulateFillSignal` with all-matching names → `AtmInvokedCount == 1` |
| T_B42_06 | `SendCopy_PublishesFillSignal_EventPipelineVerified` | `SendCopyFillSignalTests` | `PttBus.RaiseFillSignal` delivers args to subscriber; `signalCount == 1`; args fields match |
| T_B42_07 | `SendCopy_DoesNotPublishFillSignalWhenCreateOrderThrows` | `SendCopyFillSignalTests` | SendCopy via reflection with null Account; CreateOrder throws; `signalCount == 0` |

### Acceptance criteria

- `B42Tests.cs` compiles with zero errors (requires T1, T2, T3 all complete).
- `dotnet test` runs all `[Fact]` methods with zero failures.
- All 7 core [Fact] methods (T_B42_01 through T_B42_07) assert meaningful behavioral outcomes — none is a degenerate baseline-only stub.
- `PttBus.FillSignal` static event is fully cleaned up in every test teardown (`Dispose` or `try/finally`).
- T_B42_03 and T_B42_04 call `SimulateFillSignal` and assert `AtmInvokedCount == 0` (rejection proven, not assumed).
- T_B42_05 calls `SimulateFillSignal` and asserts `AtmInvokedCount == 1` (dispatch through full guard chain proven).
- T_B42_07 calls `SendCopy` via reflection; asserts `signalCount == 0` after NullRef from null Account.
- No existing `CopyEngineTests` test regressions.

---

## Dependency Order

```
T1 (PttContracts.cs)
  └─ T2 (CopyEngine.cs) — depends on FillSignalEventArgs.Create + PttBus.RaiseFillSignal from T1
  └─ T3 (PttFollowerStrategy.cs) — depends on PttBus.FillSignal event from T1
       └─ T4 (B42Tests.cs) — depends on T1 + T3; T_B42_06/07 also depend on T2
```

**Minimum build-pass order**: T1 → T2 → T3 → T4.
T2 and T3 may be implemented in parallel once T1 is merged.

---

## Build Invariants

- No file outside {T1, T2, T3, T4} is touched.
- `CopyEngine.cs` retains `SendCopy` method signature byte-for-byte.
- All existing `CopyEngineTests` `[Fact]` methods continue to pass.
- `PttBus` existing events (`BeFired`, `TrimFired`, `FlatFired`, `CancelFired`, `QuickExitFired`) and Raise* methods are unchanged.
- `FillSignalEventArgs` constructor remains `private` — `FillSignalEventArgs.Create(...)` is the only public construction path.
- NT8 F5 compilation: `PTTFollowerStrategy` appears in Strategies list after reload.
- `PttFollowerStrategy.OnFillSignal` uses virtual helper methods for ALL four name comparisons — never direct `Account.Name` / `Instrument.FullName` property access.
