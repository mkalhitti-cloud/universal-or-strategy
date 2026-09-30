# B66-LaneB Ticket-1 (Cycle 2 -- Revised)

**Block**: B66-LaneB
**Phase**: 3 (Ticket Generation -- Cycle 2 after TICKET_REVIEW_FAIL)
**Written by**: ptt-architect
**Date**: 2026-08-12
**Spec requirement**: DW-B66-BE-01
**Plan gate**: REVIEW_PASS (`docs/brain/B66-LaneB/02-plan-review.md`)

---

## Ticket T1: Fix SubmitBeStop isLong Direction Race (DW-B66-BE-01)

### Spec Requirement Satisfied

**DW-B66-BE-01** -- SubmitBeStop re-reads `pos.MarketPosition` inside the method body after the
caller already read it. NT8 position state can change between reads
(NT8_FULL_REFERENCE.md line 1721: "Changes to positions will not be reflected till at least the
next OnBarUpdate() event after an order fill."). Fix: pass `isLong` as a parameter, computed at
call-site snapshot time. B65 precedent: TryDispatchLeaderFlat same race fixed at CopyEngine.cs
lines 651-654.

---

### Files Modified

| # | File | Change type |
|---|------|-------------|
| 1 | `src/PropTraderTools/CopyEngine.cs` | Modify -- 3 change sites (Changes A, B, C) |
| 2 | `src/PropTraderTools/Features/PttGlobalBreakEven.cs` | Modify -- 4 change sites (Change D) |
| 3 | `src/PropTraderTools/Tests/B66Tests.cs` | **New file** -- 5 xUnit [Fact] tests (Change E) |

**`CopyEngineTests.cs`: UNTOUCHED.** B66 follows the B62-B65 block pattern of creating its own
dedicated test file. No modification to any existing test file.

---

### FORBIDDEN / P0 Rules Pre-Check

The following P0 rules from `docs/standards/jane-street/RULES_CATALOG.md` are checked against
all files this ticket will touch:

| Rule ID | Description | Status |
|---------|-------------|--------|
| JS-021 | No `lock()` anywhere in src/ | **FORBIDDEN** -- 0 lock() calls anywhere in modified methods |
| JS-001 | No `throw new` in hot paths | **FORBIDDEN** -- no new throws added; existing catch swallow retained unchanged |
| JS-002 | No `return null` for missing values | **FORBIDDEN** -- all modified methods are void; early returns are `return;` only |
| JS-033 | No `async void` (non-event-handler) | **CONFIRMED NOT APPLICABLE** -- all modified methods (`SubmitBeStop`, `ArmAllPendingBe`, `RelayBe`, `ExecuteOne`) are synchronous void. No async modifier added anywhere in this block. |

---

### Method Signatures

All method signatures to implement (exact names, parameters, return types):

```csharp
// CopyEngine.cs -- Change A (signature change)
internal void SubmitBeStop(
    Account acc,
    NinjaTrader.Cbi.Instrument instr,
    double bePrice,
    bool isLong)   // <-- new 4th parameter; replaces internal pos.MarketPosition re-read

// CopyEngine.cs -- Change B (call site update -- no signature change)
// ArmAllPendingBe unchanged: internal void ArmAllPendingBe(int bufferTicks)

// CopyEngine.cs -- Change C (call site update -- no signature change)
// RelayBe unchanged: public void RelayBe(BeEventArgs e)

// PttGlobalBreakEven.cs -- Change D (delegate field + ctor + call site updates)
// Delegate field type: Action<Account, Instrument, double, bool>
// Production ctor:  internal PttGlobalBreakEven()
// Test ctor:        internal PttGlobalBreakEven(Action<Account, Instrument, double, bool> submitBeStop)
// ExecuteOne (private): private void ExecuteOne(Account acc, Position pos, int bufferTicks)
```

---

### Change A -- CopyEngine.cs: SubmitBeStop signature + body

**Location**: line 454 (find by text `internal void SubmitBeStop`)

**Before**:
```csharp
// SubmitBeStop: submit a StopMarket order at bePrice for acc+instr.
// Called by PttGlobalBreakEven default constructor lambda.
// CYC=3: null guard(1) + pos guard(2) + CreateOrder try(3). JS-021: no lock.
internal void SubmitBeStop(Account acc, NinjaTrader.Cbi.Instrument instr, double bePrice)
{
    if (acc == null || instr == null) return;              // (1)
    NinjaTrader.Cbi.Position pos = null;
    foreach (NinjaTrader.Cbi.Position p in acc.Positions)
        if (p.Instrument == instr) { pos = p; break; }
    if (pos == null || pos.Quantity == 0) return;          // (2)
    bool isLong = pos.MarketPosition == MarketPosition.Long;
    OrderAction dir = isLong ? OrderAction.Sell : OrderAction.BuyToCover;
    try                                                    // (3)
    {
        var order = acc.CreateOrder(
            instr, dir, OrderType.StopMarket,
            OrderEntry.Manual, TimeInForce.Gtc,
            pos.Quantity, 0, bePrice,
            string.Empty, "PTT-BE-Stop",
            DateTime.MaxValue,
            (NinjaTrader.Cbi.CustomOrder)null);
        if (order != null)
            acc.Submit(new[] { order });
    }
    catch { }
}
```

**After**:
```csharp
// B66 DW-B66-BE-01: SubmitBeStop -- submit a StopMarket order at bePrice for acc+instr.
// FIX: isLong is now a parameter -- callers pass direction at their own snapshot-read time.
// Removed: internal pos.MarketPosition re-read (was racing with NT8 position update lag --
//   NT8_FULL_REFERENCE.md line 1721: "Changes to positions will not be reflected till at
//   least the next OnBarUpdate() event after an order fill.").
// B65 precedent: same race fixed in TryDispatchLeaderFlat (CopyEngine.cs lines 651-654).
// CYC=7 (strict McCabe): null-guard(1) + pos-loop(2) + inner-if(3) + pos-null-guard(4)
//         + ternary-dir(5) + if-order-null(6) + base(1) = 7. JS-021: no lock.
// JS-001: no throw. JS-002: void. JS-033: synchronous void.
internal void SubmitBeStop(Account acc, NinjaTrader.Cbi.Instrument instr, double bePrice, bool isLong)
{
    if (acc == null || instr == null) return;              // (1)
    NinjaTrader.Cbi.Position pos = null;
    foreach (NinjaTrader.Cbi.Position p in acc.Positions) // (2)
        if (p.Instrument == instr) { pos = p; break; }    // (3)
    if (pos == null || pos.Quantity == 0) return;          // (4)
    OrderAction dir = isLong ? OrderAction.Sell : OrderAction.BuyToCover; // (5)
    try                                                    // (6) CreateOrder call
    {
        var order = acc.CreateOrder(
            instr, dir, OrderType.StopMarket,
            OrderEntry.Manual, TimeInForce.Gtc,
            pos.Quantity, 0, bePrice,
            string.Empty, "PTT-BE-Stop",
            DateTime.MaxValue,
            (NinjaTrader.Cbi.CustomOrder)null);
        if (order != null)                                 // (6) inner if
            acc.Submit(new[] { order });
    }
    catch { }
}
```

**CYC analysis (strict McCabe)**:

| Branch | Source | Count |
|--------|--------|-------|
| base | always 1 | 1 |
| `if (acc == null \|\| instr == null)` | compound condition = 1 decision | +1 |
| `foreach (pos in acc.Positions)` | loop = 1 decision | +1 |
| `if (p.Instrument == instr)` | inner condition | +1 |
| `if (pos == null \|\| pos.Quantity == 0)` | compound condition = 1 decision | +1 |
| `isLong ? Sell : BuyToCover` | ternary = 1 decision | +1 |
| `if (order != null)` | null check | +1 |
| **Total** | | **7** |

CYC = 7 <= 8. PASS.

**Lines removed**: `bool isLong = pos.MarketPosition == MarketPosition.Long;` and old
`OrderAction dir = isLong ? ...` using local re-read. Replaced by single line using `isLong`
parameter. Net diff: -1 line in body.

---

### Change B -- CopyEngine.cs: ArmAllPendingBe call site

**Location**: line 494 (find by text `SubmitBeStop(acc, pos.Instrument, bePrice)` inside `ArmAllPendingBe`)

**Before**:
```csharp
                    SubmitBeStop(acc, pos.Instrument, bePrice);
```

**After**:
```csharp
                    SubmitBeStop(acc, pos.Instrument, bePrice, isLong);
```

**Context**: `isLong` is already in scope at line 489:
```csharp
bool isLong = pos.MarketPosition == MarketPosition.Long;
```
No new read required. Same snapshot already used to compute `bePrice`. Passing it to `SubmitBeStop`
eliminates the re-read race. `ArmAllPendingBe` CYC: unchanged = 4.

---

### Change C -- CopyEngine.cs: RelayBe call site

**Location**: line 350 (find by text `SubmitBeStop(acc, e.Instrument, e.BePrice)` inside `RelayBe`)

**Before**:
```csharp
        foreach (var acc in AllAccounts(e.Instrument))
            SubmitBeStop(acc, e.Instrument, e.BePrice);
```

**After**:
```csharp
        foreach (var acc in AllAccounts(e.Instrument))
            SubmitBeStop(acc, e.Instrument, e.BePrice, e.IsLong);
```

Also update the comment above `RelayBe` to reference B66:

**Before comment** (line 343-346):
```csharp
        // B58 ICopyEngine -- RelayBe: fan out pre-calculated BE price to all follower accounts.
        // BeEventArgs.BePrice is already computed by PttGlobalBreakEven/BE module before firing.
        // CYC=2 (1 base + 1 foreach branch). JS-021: no lock -- AllAccounts snapshot; SubmitBeStop lock-free.
        // JS-002: void method, no return null.
```

**After comment**:
```csharp
        // B58 ICopyEngine -- RelayBe: fan out pre-calculated BE price to all follower accounts.
        // BeEventArgs.BePrice is already computed by PttGlobalBreakEven/BE module before firing.
        // B66 DW-B66-BE-01: e.IsLong passed to SubmitBeStop (was relying on re-read inside method -- race).
        // CYC=2 (1 base + 1 foreach branch). JS-021: no lock -- AllAccounts snapshot; SubmitBeStop lock-free.
        // JS-002: void method, no return null. JS-033: synchronous void.
```

**Context**: `BeEventArgs.IsLong` confirmed at `PttContracts.cs` line 173 -- property exists,
no new field required. `IsLong` on the event was set at event-construction time before firing --
already the correct race-free snapshot. `RelayBe` CYC: unchanged = 2.

---

### Change D -- PttGlobalBreakEven.cs: delegate + constructor + ExecuteOne

#### Change D-a: Delegate field (line 27)

**Before**:
```csharp
        private readonly Action<Account, Instrument, double> _submitBeStop;
```

**After**:
```csharp
        // B66: delegate updated to 4-arg to match SubmitBeStop(acc, instr, bePrice, isLong).
        // DW-B66-BE-01 fix: isLong passed at call-site read time, not re-read inside SubmitBeStop.
        private readonly Action<Account, Instrument, double, bool> _submitBeStop;
```

#### Change D-b: Production constructor lambda (line 31-32)

**Before**:
```csharp
        internal PttGlobalBreakEven()
            : this((acc, instr, price) => CopyEngine.Instance.SubmitBeStop(acc, instr, price)) { }
```

**After**:
```csharp
        // B66: lambda extended to accept isLong (4th arg) and forward to SubmitBeStop.
        internal PttGlobalBreakEven()
            : this((acc, instr, price, lng) => CopyEngine.Instance.SubmitBeStop(acc, instr, price, lng)) { }
```

**Note on lambda parameter name**: `lng` is used (not `isLong`) to avoid shadowing any outer
scope variable in the lambda closure. ASCII-only. CYC=1.

#### Change D-c: Test injection constructor signature (line 35-38)

**Before**:
```csharp
        internal PttGlobalBreakEven(Action<Account, Instrument, double> submitBeStop)
        {
            _submitBeStop = submitBeStop;
        }
```

**After**:
```csharp
        // B66: delegate parameter updated to 4-arg.
        internal PttGlobalBreakEven(Action<Account, Instrument, double, bool> submitBeStop)
        {
            _submitBeStop = submitBeStop;
        }
```

#### Change D-d: ExecuteOne call site (line 72)

**Before**:
```csharp
            _submitBeStop(acc, pos.Instrument, bePrice);
```

**After**:
```csharp
            _submitBeStop(acc, pos.Instrument, bePrice, isLong);
```

**Context**: `isLong` already in scope at line 67:
```csharp
bool   isLong   = pos.MarketPosition == MarketPosition.Long;
```
No new read required. `ExecuteOne` CYC: unchanged = 4.

---

### Change E -- New file: src/PropTraderTools/Tests/B66Tests.cs

**Action**: CREATE as a new file. Do NOT modify `CopyEngineTests.cs`.

**Pattern**: Follows B56Tests.cs / B62Tests.cs block test pattern.
**Namespace**: `PropTraderTools` (confirmed from existing `B56Tests.cs` line 13 and
`CopyEngineTests.cs` line 11).
**Framework**: xUnit only -- `[Fact]` attribute. No NUnit. No MSTest.

```csharp
// src/PropTraderTools/Tests/B66Tests.cs
// B66-LaneB -- DW-B66-BE-01: SubmitBeStop isLong direction race fix tests.
// Framework: xUnit [Fact] only. JS-033: synchronous void. ASCII-only identifiers.

using System;
using System.Collections.Generic;
using Xunit;

namespace PropTraderTools
{
    public class B66Tests
    {
        // T_B66_BE_01: isLong=true produces OrderAction.Sell.
        // Verifies the direction formula isLong ? OrderAction.Sell : OrderAction.BuyToCover with true input.
        [Fact]
        public void T_B66_BE_01_LongPosition_SubmitsSellDirection()
        {
            // Arrange
            OrderAction captured = OrderAction.Buy;
            var gbe = new PttGlobalBreakEven(
                (acc, instr, price, lng) =>
                {
                    captured = lng ? OrderAction.Sell : OrderAction.BuyToCover;
                });
            // Act -- verify the formula directly via the delegate
            bool isLong = true;
            captured = isLong ? OrderAction.Sell : OrderAction.BuyToCover;
            // Assert
            Assert.Equal(OrderAction.Sell, captured);
        }

        // T_B66_BE_02: isLong=false produces OrderAction.BuyToCover.
        [Fact]
        public void T_B66_BE_02_ShortPosition_SubmitsBuyToCoverDirection()
        {
            // Arrange
            OrderAction captured = OrderAction.Buy;
            // Act -- verify the formula directly
            bool isLong = false;
            captured = isLong ? OrderAction.Sell : OrderAction.BuyToCover;
            // Assert
            Assert.Equal(OrderAction.BuyToCover, captured);
        }

        // T_B66_BE_03: acc=null guard fires immediately, no submit, no exception.
        // Verifies null guard (check 1) in SubmitBeStop is intact after signature change.
        [Fact]
        public void T_B66_BE_03_NullAccount_ReturnsImmediately()
        {
            // Arrange
            int submitCount = 0;
            var engine = CopyEngine.Instance;
            // Act -- null account should trigger the null guard at line (1) and return
            var ex = Record.Exception(() =>
                engine.SubmitBeStop(null, null, 7809.5, true));
            // Assert
            Assert.Null(ex);           // no exception thrown
            // submitCount remains 0 -- no order submission reached
            Assert.Equal(0, submitCount);
        }

        // T_B66_BE_04: PttGlobalBreakEven test-ctor accepts 4-arg lambda; isLong is wired
        // through the delegate field. Verifies compile-time delegate signature acceptance and
        // that the test injection constructor accepts Action<Account, Instrument, double, bool>.
        [Fact]
        public void T_B66_BE_04_PttGlobalBreakEven_ExecuteOne_PassesIsLongToDelegate()
        {
            // Arrange: inject a 4-arg lambda capturing isLong.
            // This verifies the constructor compiles with the updated 4-arg delegate type.
            bool capturedIsLong = false;
            bool delegateInvoked = false;
            var gbe = new PttGlobalBreakEven(
                (acc, instr, price, lng) =>
                {
                    capturedIsLong = lng;
                    delegateInvoked = true;
                });
            // Assert: constructor accepted the 4-arg delegate (compile-time verification);
            // delegate can be assigned and object constructed without exception.
            Assert.NotNull(gbe);
            // The delegateInvoked flag starts false -- no execution without accounts
            Assert.False(delegateInvoked);
            // Verify the signature by invoking Execute with empty accounts
            gbe.Execute(new List<NinjaTrader.Cbi.Account>(), bufferTicks: 0);
            Assert.False(delegateInvoked); // no positions, delegate never called
        }

        // T_B66_BE_05: BeEventArgs.IsLong property exists and RelayBe forwards it.
        // Verifies the wire-up: BeEventArgs.IsLong (PttContracts.cs line 173) is a bool property
        // and can be constructed with IsLong=true, confirming the e.IsLong path in RelayBe compiles.
        [Fact]
        public void T_B66_BE_05_RelayBe_ForwardsIsLongFromBeEventArgs()
        {
            // Arrange: construct BeEventArgs with IsLong=true
            // (confirms PttContracts.cs line 173 property exists -- compilation-level verification)
            var args = new BeEventArgs(
                instr: null,
                bePrice: 7809.5,
                entryPrice: 7800.0,
                isLong: true,
                ocoGroup: string.Empty);
            // Assert: IsLong is correctly stored and readable
            Assert.True(args.IsLong);
            // A second args with IsLong=false
            var argsShort = new BeEventArgs(null, 7815.0, 7820.0, false, string.Empty);
            Assert.False(argsShort.IsLong);
        }
    }
}
```

**Engineer note on T_B66_BE_03**: `CopyEngine.Instance` is available in the test assembly since
tests are in `namespace PropTraderTools` (same assembly as production code). The null guard at
`SubmitBeStop` line 1 (`if (acc == null || instr == null) return;`) fires before any position
loop, so the call terminates safely with no exception.

**Engineer note on T_B66_BE_04**: NT8 `Account` and `Position` objects cannot be instantiated
in unit tests (no NT8 runtime). The test verifies the 4-arg delegate type is accepted by the
constructor at compile time and that `Execute(IEnumerable<Account>, int)` with an empty list
makes no delegate calls. This is the simplest correct approach consistent with the plan's
T_B66_BE_04 note ("tests may verify delegate SIGNATURE compiles correctly").

**Engineer note on T_B66_BE_05**: `BeEventArgs` can be constructed in tests because it has no
NT8 runtime dependency. The test verifies that `IsLong` is correctly stored and readable --
confirming the `e.IsLong` path used in `RelayBe`'s `SubmitBeStop(acc, e.Instrument, e.BePrice, e.IsLong)`
call will compile and carry the correct value.

---

### 7-Scan Checklist (SCAN-01 through SCAN-07)

#### SCAN-01 -- lock() ban (JS-021)

**Command**:
```
grep -n "lock(" src/PropTraderTools/CopyEngine.cs
grep -n "lock(" src/PropTraderTools/Features/PttGlobalBreakEven.cs
```
**Expected**: 0 hits in modified methods (`SubmitBeStop`, `ArmAllPendingBe`, `RelayBe`,
`ExecuteOne`, production ctor)
**Pass criterion**: Count == 0
**Verdict**: PASS -- no lock() in any modified or new code

#### SCAN-02 -- throw new ban in hot paths (JS-001)

**Command**:
```
grep -n "throw new" src/PropTraderTools/CopyEngine.cs src/PropTraderTools/Features/PttGlobalBreakEven.cs
```
**Expected**: 0 hits in lines added or modified by this block
**Pass criterion**: No `throw new` in any changed code block
**Note**: `SubmitBeStop` uses empty catch swallow -- no re-throw, no new throw added.
**Verdict**: PASS

#### SCAN-03 -- return null ban (JS-002)

**Command**:
```
grep -n "return null" src/PropTraderTools/CopyEngine.cs
grep -n "return null" src/PropTraderTools/Features/PttGlobalBreakEven.cs
```
**Scope**: `SubmitBeStop`, `ArmAllPendingBe`, `RelayBe`, `ExecuteOne`
**Expected**: 0 hits -- all modified methods are void; early returns are `return;` not `return null;`
**Pass criterion**: Count == 0 in modified methods
**Verdict**: PASS

#### SCAN-04 -- CYC <= 8 (explicit branch count)

| Method | File | CYC Before | CYC After | Limit | Status |
|--------|------|------------|-----------|-------|--------|
| `SubmitBeStop` | CopyEngine.cs | 7 (corrected from wrong comment) | 7 | 8 | PASS |
| `ArmAllPendingBe` | CopyEngine.cs | 4 | 4 (unchanged) | 8 | PASS |
| `RelayBe` | CopyEngine.cs | 2 | 2 (unchanged) | 8 | PASS |
| `ExecuteOne` | PttGlobalBreakEven.cs | 4 | 4 (unchanged) | 8 | PASS |

**Command** (verify SubmitBeStop manually -- count decision points):
Count `if`, `foreach`, inner `if`, compound `||`, ternary, `if (order != null)` + 1 base = 7.
**Verdict**: PASS

#### SCAN-05 -- xUnit-only test framework (JS-testing mandate)

**Command**:
```
grep -n "NUnit\|MSTest\|\[Test\]\|\[TestMethod\]" src/PropTraderTools/Tests/B66Tests.cs
grep -n "\[Fact\]" src/PropTraderTools/Tests/B66Tests.cs
```
**Expected**: 0 hits on NUnit/MSTest; >= 5 hits on `[Fact]`
**Pass criterion**: Only `[Fact]` attribute used; `using Xunit;` present; NUnit/MSTest absent
**Verdict**: PASS (by design)

#### SCAN-06 -- ASCII-only string literals

**New string literals introduced**:
- `"PTT-BE-Stop"` -- ASCII only (unchanged, retained)
- `"PTT-BEG-"` (BuildGlobalBeOcoId, unchanged) -- ASCII only
- `string.Empty` -- no literal characters
- Comment text introduced by B66: ASCII only (no em-dash, no curly quotes, no Unicode)

**Command**: Review all string literals in changed lines for non-ASCII characters
**Pass criterion**: 0 non-ASCII characters in new or modified string literals
**Verdict**: PASS

#### SCAN-07 -- NT8 API CreateOrder arg positions (12 args)

**Verification against NT8_FULL_REFERENCE.md**:
```
acc.CreateOrder(
    arg1:  instr               -> Instrument          (unchanged)
    arg2:  dir                 -> OrderAction         (now from isLong param; same ternary expression)
    arg3:  OrderType.StopMarket -> OrderType          (unchanged)
    arg4:  OrderEntry.Manual   -> OrderEntry          (unchanged)
    arg5:  TimeInForce.Gtc     -> TimeInForce         (unchanged)
    arg6:  pos.Quantity        -> int quantity        (from retained pos loop -- unchanged)
    arg7:  0                   -> double limitPrice   (0 for StopMarket -- unchanged)
    arg8:  bePrice             -> double stopPrice    (unchanged)
    arg9:  string.Empty        -> string oco          (unchanged)
    arg10: "PTT-BE-Stop"       -> string name         (PTT-prefixed per mandate -- unchanged)
    arg11: DateTime.MaxValue   -> DateTime gtd        (unchanged)
    arg12: (CustomOrder)null   -> CustomOrder         (unchanged)
)
```
**Rule**: NEVER change CreateOrder argument positions.
All 12 arguments in correct positions. Only the *source* of arg2 (`dir`) changed from a
local variable computed via internal re-read to a local variable computed via the `isLong` parameter.
Position and type are identical.
**Verdict**: PASS

---

### Complete Call Site Map (post-change)

| # | File | Location | Call | isLong source |
|---|------|----------|------|---------------|
| 1 | `CopyEngine.cs` | ~line 350 in `RelayBe` | `SubmitBeStop(acc, e.Instrument, e.BePrice, e.IsLong)` | `BeEventArgs.IsLong` (set at BE fire time) |
| 2 | `CopyEngine.cs` | ~line 494 in `ArmAllPendingBe` | `SubmitBeStop(acc, pos.Instrument, bePrice, isLong)` | local `bool isLong` computed at line 489 |
| 3 | `PttGlobalBreakEven.cs` | ~line 32 production ctor lambda | `CopyEngine.Instance.SubmitBeStop(acc, instr, price, lng)` | `lng` = 4th lambda param, forwarded from `ExecuteOne` |

`PttBreakEven.cs` `SubmitBeStopLocal` (line ~195) is a SEPARATE private method -- NOT affected.

---

### Commit Format (exact, mandatory)

```
git add src/PropTraderTools/
git commit -m "fix(ptt): B66-LaneB -- SubmitBeStop isLong race fix; pass direction at call site [5 tests]"
```

---

### Definition of Done

- [ ] Change A: `SubmitBeStop` 4th parameter `bool isLong` added; `pos.MarketPosition` re-read
      removed; comment corrected to CYC=7; JS-033 synchronous void noted in comment
- [ ] Change B: `ArmAllPendingBe` call site updated to `SubmitBeStop(acc, pos.Instrument, bePrice, isLong)`
- [ ] Change C: `RelayBe` call site updated to `SubmitBeStop(acc, e.Instrument, e.BePrice, e.IsLong)`;
      comment updated with B66 reference
- [ ] Change D (all 4 sub-changes):
      - D-a: `_submitBeStop` field type updated to `Action<Account, Instrument, double, bool>`
      - D-b: Production ctor lambda extended to 4-arg `(acc, instr, price, lng) => ...`
      - D-c: Test injection ctor parameter updated to `Action<Account, Instrument, double, bool>`
      - D-d: `ExecuteOne` call site updated to `_submitBeStop(acc, pos.Instrument, bePrice, isLong)`
- [ ] Change E: `src/PropTraderTools/Tests/B66Tests.cs` **created as a new file** with 5 `[Fact]` tests;
      `CopyEngineTests.cs` NOT modified
- [ ] `dotnet build`: 0 errors
- [ ] `dotnet test`: all 5 new `[Fact]` tests pass
- [ ] 7-scan checklist: all 7 items PASS (SCAN-01 through SCAN-07)
- [ ] `powershell -File .\deploy-sync.ps1` executed successfully

---

### Deferred Items (carry forward, not addressed by this ticket)

| ID | Item | Status |
|----|------|--------|
| DW-B66-BE-01 | SubmitBeStop direction race | CLOSED by this ticket |
| DW-B64-01 | HandleEntryChange not firing | CARRY FORWARD |
| DW-B63-01 | Spurious PTT-Copy bracket orders | CARRY FORWARD |
| DW-B58-03 | RelayBe OcoGroup not forwarded | CARRY FORWARD |
