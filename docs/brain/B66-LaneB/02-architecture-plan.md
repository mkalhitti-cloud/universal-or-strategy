# B66-LaneB Architecture Plan

**Block**: B66-LaneB
**Phase**: 1 (Architecture Planning)
**Written by**: ptt-architect
**Date**: 2026-08-12
**Status**: REVIEW_PENDING
**Bug ref**: DW-B66-BE-01

---

## Rules Catalog Gate Result

**GATE: PASS**

Rules checked against files this task will touch (`CopyEngine.cs`, `PttGlobalBreakEven.cs`,
new `B66Tests.cs`):

| Rule ID | Description | Result |
|---------|-------------|--------|
| JS-021 | No `lock()` anywhere in src/ | PASS — grep confirms zero lock() in SubmitBeStop/ArmAllPendingBe/RelayBe/ExecuteOne |
| JS-001 | No `throw new` in hot paths | PASS — no throw added; existing try/catch swallow retained unchanged |
| JS-002 | No `return null` | PASS — all modified methods are void |
| JS-033 | No `async void` | PASS — all methods are synchronous void |
| JS-036/037 | No heap allocation in hot path | PASS — no new `byte[]` or pooled array needed |

No P0 violations found. Work may proceed.

---

## Bug Summary

**DW-B66-BE-01** — PTT-BE-Stop orders rejected on Long positions.

**Symptom observed**: Orders rejected with "buy order stop price must be above trade price" on Long
positions (e.g. entry 7809.5, stop submitted at 7803.75). NT8 correctly rejects a BuyToCover stop
submitted below market — meaning a Long position was treated as Short internally.

**Root cause**: `SubmitBeStop` (`CopyEngine.cs` line 454) re-reads `pos.MarketPosition` at line 461
inside the method body to determine `OrderAction dir`. `ArmAllPendingBe` (line 481) already read
`pos.MarketPosition` correctly at line 489 and used it to compute `bePrice`. Between the two reads,
NT8's position state can change.

**NT8 race authority**: `NT8_FULL_REFERENCE.md` line 1721:
> "Changes to positions will not be reflected till at least the next OnBarUpdate() event after an order fill."

A fill can update `pos.MarketPosition` to `Flat` between the two reads, causing `isLong=false` on a
Long position — `BuyToCover` is submitted at a below-market stop price and NT8 rejects it.

**B65 precedent**: Identical race fixed in `TryDispatchLeaderFlat` by passing `e.Order.Name` at
call-site read time instead of re-reading inside the method (CopyEngine.cs lines 651-654).

---

## Affected Files

| # | File | Change type |
|---|------|-------------|
| 1 | `src/PropTraderTools/CopyEngine.cs` | Modify — 3 change sites (signature, 2 call sites) |
| 2 | `src/PropTraderTools/Features/PttGlobalBreakEven.cs` | Modify — 4 change sites (delegate type + 3 usages) |
| 3 | `src/PropTraderTools/Tests/B66Tests.cs` | New — 5 xUnit [Fact] tests |

**No other files changed.** `PttContracts.cs` is read-only for this block — `BeEventArgs.IsLong`
already exists at line 173 (confirmed). `CopyEngineTests.cs` is untouched — B66 gets its own test
file following the B62-B65 block pattern.

---

## Architect Decision: qty parameter vs. retained pos loop

**Question posed in spec**: Pass `int qty` as a 5th parameter, OR keep the pos loop inside
`SubmitBeStop` for qty-only?

**Decision: Keep the pos loop, remove only the MarketPosition re-read.**

**Rationale**:
1. **Minimal diff**: The pos loop already exists. Removing it would require `RelayBe` to also
   resolve qty before calling `SubmitBeStop`, which grows the diff and risks introducing a separate
   race on qty.
2. **Different race profile**: `pos.Quantity` going to 0 is guarded by `if (pos.Quantity == 0) return`
   at the ArmAllPendingBe call site (line 488) before `SubmitBeStop` is invoked — and the guard is
   inside SubmitBeStop itself (check 2). Quantity-0 is a soft guard; MarketPosition mismatch is a
   hard rejection. The Quantity re-read inside SubmitBeStop has a harmless failure mode (guard
   catches flat pos); the MarketPosition re-read has a fatal failure mode (wrong direction = NT8
   rejection).
3. **NT8 scan precedent**: B65 fixed direction by passing the value. The minimal surgery is:
   remove the direction re-read, accept direction as a parameter. Keep everything else identical.

---

## Change 1 — CopyEngine.cs: SubmitBeStop signature + body

### Before (lines 454-476)

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

### After

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

**Lines removed**: The two lines `bool isLong = pos.MarketPosition == MarketPosition.Long;` and
the old `OrderAction dir = isLong ? OrderAction.Sell : OrderAction.BuyToCover;` are collapsed into
one line using the `isLong` parameter directly. Net diff: -1 line in body.

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

---

## Change 2 — CopyEngine.cs: ArmAllPendingBe call site

**Location**: line 494 (subject to minor shift from ongoing block edits; find by text).

### Before

```csharp
                    SubmitBeStop(acc, pos.Instrument, bePrice);
```

### After

```csharp
                    SubmitBeStop(acc, pos.Instrument, bePrice, isLong);
```

**Context**: `isLong` is already in scope at this point — declared at line 489:
```csharp
bool isLong = pos.MarketPosition == MarketPosition.Long;
```
No new read required. This is the snapshot that was already used to compute `bePrice`. Passing the
same value to `SubmitBeStop` eliminates the re-read race.

**ArmAllPendingBe CYC**: Unchanged. Still CYC=4.

---

## Change 3 — CopyEngine.cs: RelayBe call site

**Location**: line 350 (subject to minor shift; find by text inside `RelayBe`).

### Before

```csharp
        foreach (var acc in AllAccounts(e.Instrument))
            SubmitBeStop(acc, e.Instrument, e.BePrice);
```

### After

```csharp
        foreach (var acc in AllAccounts(e.Instrument))
            SubmitBeStop(acc, e.Instrument, e.BePrice, e.IsLong);
```

**Context**: `BeEventArgs.IsLong` is confirmed at `PttContracts.cs` line 173 — property exists,
no new field required. The `IsLong` on the event was set at event-construction time, at the moment
the BE module read direction — before the event was fired. This is already the correct, race-free
snapshot.

**RelayBe CYC**: Unchanged. Still CYC=2.

---

## Change 4 — PttGlobalBreakEven.cs: delegate + constructor + ExecuteOne

### Change 4a — Delegate field (line 27)

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

### Change 4b — Production constructor lambda (line 32)

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

### Change 4c — Test injection constructor signature (line 35)

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

### Change 4d — ExecuteOne call site (line 72)

**Before**:
```csharp
        _submitBeStop(acc, pos.Instrument, bePrice);
```

**After**:
```csharp
        _submitBeStop(acc, pos.Instrument, bePrice, isLong);
```

**Context**: `isLong` is already in scope — declared at line 67:
```csharp
bool   isLong   = pos.MarketPosition == MarketPosition.Long;
```
No new read required. `ExecuteOne` CYC: unchanged = 4.

---

## Call Site Map (complete)

Every call to `SubmitBeStop` in the codebase after change:

| # | File | Location | Call | isLong source |
|---|------|----------|------|---------------|
| 1 | `CopyEngine.cs` | line ~350 in `RelayBe` | `SubmitBeStop(acc, e.Instrument, e.BePrice, e.IsLong)` | `BeEventArgs.IsLong` (set at BE fire time) |
| 2 | `CopyEngine.cs` | line ~494 in `ArmAllPendingBe` | `SubmitBeStop(acc, pos.Instrument, bePrice, isLong)` | local `bool isLong` computed at line 489 |
| 3 | `PttGlobalBreakEven.cs` | line ~32 production ctor lambda | `CopyEngine.Instance.SubmitBeStop(acc, instr, price, lng)` | `lng` = 4th lambda param, passed by ExecuteOne |

`PttBreakEven.cs` has its own `SubmitBeStopLocal` (line 195) — this is a SEPARATE private method,
not `CopyEngine.SubmitBeStop`. It is NOT affected by this change.

---

## Signature Summary Table

| Symbol | Before | After |
|--------|--------|-------|
| `CopyEngine.SubmitBeStop` | `internal void SubmitBeStop(Account acc, Instrument instr, double bePrice)` | `internal void SubmitBeStop(Account acc, Instrument instr, double bePrice, bool isLong)` |
| `PttGlobalBreakEven._submitBeStop` | `Action<Account, Instrument, double>` | `Action<Account, Instrument, double, bool>` |
| `PttGlobalBreakEven(Action<...>)` ctor | `(Action<Account, Instrument, double>)` | `(Action<Account, Instrument, double, bool>)` |
| `PttGlobalBreakEven.ExecuteOne` (call) | `_submitBeStop(acc, pos.Instrument, bePrice)` | `_submitBeStop(acc, pos.Instrument, bePrice, isLong)` |

---

## Test Plan

**Test file**: `src/PropTraderTools/Tests/B66Tests.cs` (new file)
**Framework**: xUnit only — `[Fact]` attribute, no NUnit/MSTest
**Pattern**: Follows `B65Tests.cs` block-test pattern (same namespace, same assembly)

### T_B66_BE_01 — Long position routes Sell direction

```
[Fact]
public void T_B66_BE_01_LongPosition_SubmitsSellDirection()
```
- **Setup**: Stub `Account` + `Position` with `MarketPosition.Long`, `Quantity=1`
- **Action**: Call `SubmitBeStop(stubAcc, instr, 7809.5, isLong: true)`
- **Assert**: Captured `OrderAction` == `OrderAction.Sell`
- **Rationale**: Verifies that `isLong=true` produces `Sell` — the correct stop-loss direction for Long

### T_B66_BE_02 — Short position routes BuyToCover direction

```
[Fact]
public void T_B66_BE_02_ShortPosition_SubmitsBuyToCoverDirection()
```
- **Setup**: Stub `Account` + `Position` with `MarketPosition.Short`, `Quantity=1`
- **Action**: Call `SubmitBeStop(stubAcc, instr, 7815.0, isLong: false)`
- **Assert**: Captured `OrderAction` == `OrderAction.BuyToCover`
- **Rationale**: Verifies that `isLong=false` produces `BuyToCover` — correct direction for Short

### T_B66_BE_03 — Null account guard returns without submitting

```
[Fact]
public void T_B66_BE_03_NullAccount_NoOrderSubmitted()
```
- **Setup**: `submitCount = 0`; inject spy delegate
- **Action**: Call `SubmitBeStop(null, instr, 7809.5, isLong: true)`
- **Assert**: `submitCount == 0` (no submit call made); no exception thrown
- **Rationale**: Verifies the null-guard (check 1) is still intact after signature change

### T_B66_BE_04 — PttGlobalBreakEven ExecuteOne passes isLong to delegate

```
[Fact]
public void T_B66_BE_04_ExecuteOne_PassesIsLongToDelegate()
```
- **Setup**: Create `PttGlobalBreakEven` with captured-delegate injection:
  ```csharp
  bool capturedIsLong = false;
  var gbe = new PttGlobalBreakEven(
      (acc, instr, price, lng) => { capturedIsLong = lng; });
  ```
  Mock `Account` with one Long position (MarketPosition.Long, Quantity=1, AveragePrice=100.0)
- **Action**: `gbe.Execute(new[] { mockAcc }, bufferTicks: 0)`
- **Assert**: `capturedIsLong == true`
- **Rationale**: Verifies the test-seam path properly wires `isLong` through `ExecuteOne` to the delegate

### T_B66_BE_05 — RelayBe forwards IsLong from BeEventArgs

```
[Fact]
public void T_B66_BE_05_RelayBe_ForwardsIsLongFromEvent()
```
- **Setup**: Create `BeEventArgs` with `IsLong=true`, `BePrice=7809.5`, a test instrument, and
  a spy account that records the `OrderAction` passed to `CreateOrder`
- **Action**: Call `CopyEngine.Instance.RelayBe(e)` via test-accessible overload, OR use a
  `FakeCopyEngine` wrapper that captures `isLong` passed to the internal `SubmitBeStop` call
  via delegate injection on the test-configured engine instance
- **Assert**: Captured `isLong == true` confirming `e.IsLong` was forwarded
- **Rationale**: End-to-end path from `BeEventArgs.IsLong` through `RelayBe` to `SubmitBeStop`
- **Implementation note**: If `CopyEngine` is difficult to isolate for this test, an acceptable
  alternative is testing `RelayBe` by verifying the compiled method body via source inspection
  in the code review, and covering the functional behavior in T_B66_BE_01 and T_B66_BE_02.
  The engineer MUST consult the ptt-ticket-reviewer if the isolation strategy is unclear.

---

## CYC Analysis — All Changed Methods

| Method | File | CYC Before | CYC After | Limit | Status |
|--------|------|------------|-----------|-------|--------|
| `SubmitBeStop` | CopyEngine.cs | 7 (actual; comment said 3) | 7 | 8 | PASS |
| `ArmAllPendingBe` | CopyEngine.cs | 4 | 4 (unchanged) | 8 | PASS |
| `RelayBe` | CopyEngine.cs | 2 | 2 (unchanged) | 8 | PASS |
| `ExecuteOne` | PttGlobalBreakEven.cs | 4 | 4 (unchanged) | 8 | PASS |
| `Execute(IEnumerable<Account>, int)` | PttGlobalBreakEven.cs | 5 | 5 (unchanged) | 8 | PASS |

**Note on SubmitBeStop CYC discrepancy**: The existing comment at line 453 declares `CYC=3` but
the strict McCabe count (1 base + 6 decision points) is 7. The comment was incorrect from
the original implementation. The plan corrects the comment to `CYC=7`. The *change to the body
does not increase CYC* — removing the `pos.MarketPosition` assignment (not a branch) and replacing
`OrderAction dir = isLong ? ...` with the same ternary using the parameter value leaves the
decision-point count identical.

---

## 7-Scan Checklist

### SCAN-01 — lock() ban

**Command**: `grep -n "lock(" src/PropTraderTools/CopyEngine.cs`
**Expected**: 0 hits in new/modified methods (`SubmitBeStop`, `ArmAllPendingBe`, `RelayBe`)
**Pass criterion**: Count == 0
**Source verification**: Current body uses `foreach` + `Interlocked` elsewhere. No lock() in scope.
**Verdict**: PASS

### SCAN-02 — throw new ban in hot paths

**Command**: `grep -n "throw new" src/PropTraderTools/CopyEngine.cs src/PropTraderTools/Features/PttGlobalBreakEven.cs`
**Expected**: 0 hits in the lines added or modified by this block
**Pass criterion**: No `throw new` in any changed code block
**Source verification**: `SubmitBeStop` uses try/catch swallow (no re-throw). No throw added.
**Verdict**: PASS

### SCAN-03 — return null ban

**Command**: `grep -n "return null" src/PropTraderTools/CopyEngine.cs` (scope: SubmitBeStop, ArmAllPendingBe, RelayBe)
**Expected**: 0 hits (all methods are void; early returns are `return;` not `return null;`)
**Pass criterion**: Count == 0 in modified methods
**Verdict**: PASS

### SCAN-04 — CYC <= 8

**Explicit branch count** (see CYC Analysis table above):
- `SubmitBeStop` after change: 7 <= 8 PASS
- `ArmAllPendingBe`: 4 <= 8 PASS (unchanged)
- `RelayBe`: 2 <= 8 PASS (unchanged)
- `ExecuteOne`: 4 <= 8 PASS (unchanged)
**Verdict**: PASS

### SCAN-05 — xUnit-only test framework

**Command**: `grep -n "NUnit\|MSTest\|\[Test\]\|\[TestMethod\]" src/PropTraderTools/Tests/B66Tests.cs`
**Expected**: 0 hits
**Pass criterion**: Only `[Fact]` attribute used; `using Xunit;` in using directives
**Verdict**: PASS (by design — test file uses xUnit only)

### SCAN-06 — ASCII-only string literals

**Command**: Check all new string literals in changed blocks for non-ASCII characters
**Strings added or retained**:
- `"PTT-BE-Stop"` — ASCII only
- `"PTT-BEG-"` (in `BuildGlobalBeOcoId`, unchanged) — ASCII only
- Comment text: ASCII only (no em-dash, no curly quotes, no Unicode arrows added by this block)
**Pass criterion**: 0 non-ASCII characters in new string literals
**Verdict**: PASS

### SCAN-07 — NT8 API CreateOrder arg positions

**Command**: Manual verification of `acc.CreateOrder(...)` arg positions against NT8_FULL_REFERENCE.md
**Verification**:
```
CreateOrder(
    arg1:  instr           → Instrument         (parameter name: instrument)
    arg2:  dir             → OrderAction        (now from isLong param, same ternary)
    arg3:  StopMarket      → OrderType
    arg4:  Manual          → OrderEntry
    arg5:  Gtc             → TimeInForce
    arg6:  pos.Quantity    → int quantity       (from retained pos loop — unchanged)
    arg7:  0               → double limitPrice  (0 for StopMarket)
    arg8:  bePrice         → double stopPrice
    arg9:  string.Empty    → string oco
    arg10: "PTT-BE-Stop"   → string name        (PTT-prefixed per mandate)
    arg11: DateTime.MaxValue → DateTime gtd
    arg12: (CustomOrder)null → CustomOrder customOrder
)
```
All 12 arguments in correct positions. No arg moved. `dir` (arg2) uses the same ternary expression,
now sourced from the `isLong` parameter instead of a local re-read.
**Verdict**: PASS

---

## Risk Assessment

### Risk 1 — Pattern proven by B65

**Level**: LOW
The B65 block fixed an identical NT8 position-state race by passing a value captured at call-site
snapshot-read time rather than re-reading inside the helper. The fix for `TryDispatchLeaderFlat`
(CopyEngine.cs lines 651-654) is structurally identical to the fix for `SubmitBeStop`. No new
architectural pattern is introduced.

### Risk 2 — PttGlobalBreakEven delegate chain

**Level**: LOW
The delegate type change cascades through: field type → production ctor lambda → test ctor
→ ExecuteOne call site. All 4 change sites are identified and bounded. The `Execute(int)` production
path delegates to `ArmAllPendingBe` (not to `_submitBeStop` directly) — the `_submitBeStop`
delegate is only invoked in the test-seam `Execute(IEnumerable<Account>, int)` overload. The
production constructor lambda wires the delegate correctly and the lambda compiles at C# level.

### Risk 3 — SubmitBeStop CYC comment correction

**Level**: INFO (not a risk)
The existing CYC=3 comment on `SubmitBeStop` is wrong (true count is 7). The B66 change corrects
this to `CYC=7` in the comment. The actual behavior of the method is unchanged in this regard —
no new branches are introduced. The ptt-verifier should confirm the corrected comment matches
strict McCabe count.

### Risk 4 — Deferred OPEN items from B65

**Level**: NOT ADDRESSED (by design)
DW-B64-01 (`HandleEntryChange` not firing) and DW-B63-01 (spurious PTT-Copy bracket orders) remain
OPEN and carry forward. This block addresses ONLY DW-B66-BE-01. No scope creep.

---

## Data Flow Diagram

```
Path A (Global BE button press):
  TradeCopierPanel.OnGlobalBeClick
    → PttGlobalBreakEven.Execute(bufferTicks)
    → CopyEngine.ArmAllPendingBe(bufferTicks)
        foreach acc in Account.All
          foreach pos in acc.Positions
            bool isLong = pos.MarketPosition == Long  ← snapshot HERE
            double bePrice = ...
            SubmitBeStop(acc, pos.Instrument, bePrice, isLong)  ← B66 fix: isLong passed
              OrderAction dir = isLong ? Sell : BuyToCover      ← uses parameter, no re-read
              acc.CreateOrder(..., dir, ..., bePrice, ...)
              acc.Submit(new[] { order })

Path B (BE event relay):
  PttBreakEven fires BeEvent with BeEventArgs(IsLong=true/false, ...)
    → CopyEngine.RelayBe(e)
        foreach acc in AllAccounts(e.Instrument)
          SubmitBeStop(acc, e.Instrument, e.BePrice, e.IsLong)  ← B66 fix: e.IsLong passed
            OrderAction dir = isLong ? Sell : BuyToCover        ← uses parameter, no re-read

Path C (test seam — unit tests only):
  PttGlobalBreakEven.Execute(mockAccounts, bufferTicks)
    foreach acc / foreach pos
      ExecuteOne(acc, pos, bufferTicks)
        bool isLong = pos.MarketPosition == Long  ← snapshot in ExecuteOne
        _submitBeStop(acc, pos.Instrument, bePrice, isLong)  ← B66 fix: isLong passed
          [captured by test spy lambda]
```

---

## Deferred Backlog Impact

| ID | Item | Action |
|----|------|--------|
| DW-B66-BE-01 | SubmitBeStop direction race (this block) | CLOSED by B66-LaneB |
| DW-B64-01 | HandleEntryChange not firing | CARRY FORWARD — not addressed |
| DW-B63-01 | Spurious PTT-Copy bracket orders | CARRY FORWARD — not addressed |
| DW-B58-03 | RelayBe OcoGroup not forwarded | CARRY FORWARD — not addressed |

---

## Plan Status

**REVIEW_PENDING** — awaiting ptt-plan-reviewer (Phase 2) verification.

Return value (post-review): `REVIEW_PASS` or `REVIEW_FAIL: <violation>`
