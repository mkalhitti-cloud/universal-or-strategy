# B67-LaneA Architecture Plan
## DW-B67-01 — FlattenOneAccount: cancel follower ATM+QX brackets before market close order

**Block**: B67-LaneA  
**DW Item**: DW-B67-01 (P0)  
**Status**: REVIEW_PENDING  
**Author**: ptt-architect  
**Date**: 2026-08-13  

---

## Section 1 — Problem Statement

### DW-B67-01 Root Cause

When the leader clicks Close in ChartTrader, `CopyEngine.FlattenOneAccount` is invoked for
each follower account. The current implementation calls `acc.CreateOrder(Market...)` immediately
after the position null/quantity guard, without first cancelling any live bracket orders on the
follower account.

At Rithmic/Apex the incoming market-close order arrives at the broker layer while an active OCO
bracket (Stop + Target, submitted by CopyEngine QX or ATM auto-inject) is still alive. The broker
rejects the flatten attempt because the conflicting OCO bracket is in Working state. NT8 logs:

```
Close operation failed. Operation timed out.
```

The follower position is NOT closed. Confirmed in live trading 2026-08-12.

### Root Cause Summary

| Layer | Cause |
|---|---|
| Broker (Rithmic/Apex) | Market order conflicts with live OCO bracket on same instrument |
| NT8 CopyEngine | `acc.CreateOrder` submitted before `acc.Cancel` for bracket orders |
| Fix required | Call `CancelQxBrackets(acc, instrument)` before `acc.CreateOrder` |

### Scope

This fix is confined to `FlattenOneAccount` (lines 1423–1446) and the caller-list comment on
`CancelQxBrackets` (line 443). No logic changes to `CancelQxBrackets` itself.

---

## Section 2 — NT8 API Evidence

### CancelQxBrackets (already exists, CopyEngine.cs line 447)

```csharp
internal void CancelQxBrackets(Account acc, NinjaTrader.Cbi.Instrument instr)
```

- Cancels all `Working | Initialized | Accepted` orders on `acc` for `instr` where:
  - `IsAtmBracketName(o)` is true (Stop1/Stop2/Target1/Target2), OR
  - `IsQxCancelCandidate(o)` is true (PTT-QX-*, PTT-BE-* orders)
- Calls `acc.Cancel(stale.ToArray())` — NT8 Account.Cancel(Order[])
- Swallows all exceptions via bare `catch { }` — safe for use inside FlattenOneAccount
- CYC=6 (unchanged). JS-021 compliant (no lock).
- **Type compatibility**: `FlattenOneAccount` receives `Instrument instrument`; `CancelQxBrackets`
  accepts `NinjaTrader.Cbi.Instrument instr`. In NT8 scope `Instrument` is `NinjaTrader.Cbi.Instrument`.
  Passing `instrument` directly to `CancelQxBrackets(acc, instrument)` is type-compatible. Confirmed
  from existing callers (`PttQuickExit.Execute` passes an `Instrument` directly).

### acc.CreateOrder (NT8 Account method — already used at line 1436)

```csharp
acc.CreateOrder(
    instrument, action, OrderType.Market, OrderEntry.Manual,
    TimeInForce.Gtc, pos.Quantity, 0, 0, null, "PTT-Flatten",
    DateTime.MaxValue, null);
```

- This is the existing call. No changes to its arguments or position in the method.
- It remains inside the existing try/catch block.

### NT8 Precedent — cancel-before-flatten pattern

```
@2Custom-0909edcc FlattenPositionByName V8.31 comment:
  "Cancel ALL bracket orders first to prevent race conditions."
```

This is the canonical NT8 pattern for safe position flattening when bracket orders may be live.

### Threading Model

`CancelQxBrackets` and `acc.CreateOrder` are NT8 Cbi API calls. Both are safe on the NT8
dispatcher thread (the thread on which `OnOrderUpdate`/`OnPositionUpdate` events are dispatched).
`FlattenOneAccount` is called from within that event chain. No `Dispatcher.InvokeAsync` is needed
for Cbi calls. `StatusUpdate?.Invoke` events are handled by `TradeCopierPanel` which marshals
internally via `Dispatcher.InvokeAsync` — that is a UI concern, not a CopyEngine concern.

No `lock()` is used anywhere in the modified path. JS-021 PASS.

---

## Section 3 — Proposed Change

### 3.1 Change A — FlattenOneAccount comment block (lines 1423–1424) REPLACE

**BEFORE** (lines 1423–1424):
```csharp
        // B28 T1 -- FlattenOneAccount: per-account market flatten helper. CYC=3.
        // (1) pos null/qty guard, (2) action ternary, (3) try/catch CreateOrder.
```

**AFTER**:
```csharp
        // B28 T1 -- FlattenOneAccount: per-account market flatten helper.
        // B67 DW-B67-01: cancel follower ATM+QX brackets BEFORE submitting market order.
        // NT8 precedent: @2Custom-0909edcc FlattenPositionByName V8.31 comment:
        //   "Cancel ALL bracket orders first to prevent race conditions."
        // Rithmic/Apex: incoming market order conflicts with live OCO bracket at broker layer
        //   -> "Close operation failed. Operation timed out." without this cancel step.
        // CYC=4: (1) pos null/qty guard, (2) CancelQxBrackets, (3) action ternary, (4) try/catch.
        // JS-021: no lock. JS-001: no throw in hot path. JS-002: void.
```

### 3.2 Change B — CancelQxBrackets call-site INSERT (after line 1432, before line 1433)

Insert the following single line immediately after the early-return guard block and before the
`var action` ternary:

```csharp
            CancelQxBrackets(acc, instrument);   // B67 DW-B67-01: cancel before market order
```

**Exact placement**:
- After: `}` closing brace of the `if (pos == null || pos.Quantity == 0)` block (current line 1432)
- Before: `var action = pos.MarketPosition == ...` (current line 1433)

### 3.3 Change C — CancelQxBrackets caller-list comment update (lines 443–445) REPLACE

**BEFORE** (lines 443–445):
```csharp
        // CancelQxBrackets: cancel all Working/Initialized/Accepted ATM-bracket + PTT-* orders on acc for instr.
        // Called by PttQuickExit.Execute() before re-placing new bracket.
        // CYC=6: null guard(1) + foreach(2) + stateOk(3) + instrument check(4) + IsQxCancelCandidate(5) + staleCount(6).
```

**AFTER**:
```csharp
        // CancelQxBrackets: cancel all Working/Initialized/Accepted ATM-bracket + PTT-* orders on acc for instr.
        // Called by PttQuickExit.Execute() before re-placing new bracket.
        // Called by FlattenOneAccount (B67 DW-B67-01) to cancel brackets before market flatten.
        // CYC=6: null guard(1) + foreach(2) + stateOk(3) + instrument check(4) + IsQxCancelCandidate(5) + staleCount(6).
```

### 3.4 Complete Updated FlattenOneAccount (target shape for engineer)

```csharp
        // B28 T1 -- FlattenOneAccount: per-account market flatten helper.
        // B67 DW-B67-01: cancel follower ATM+QX brackets BEFORE submitting market order.
        // NT8 precedent: @2Custom-0909edcc FlattenPositionByName V8.31 comment:
        //   "Cancel ALL bracket orders first to prevent race conditions."
        // Rithmic/Apex: incoming market order conflicts with live OCO bracket at broker layer
        //   -> "Close operation failed. Operation timed out." without this cancel step.
        // CYC=4: (1) pos null/qty guard, (2) CancelQxBrackets, (3) action ternary, (4) try/catch.
        // JS-021: no lock. JS-001: no throw in hot path. JS-002: void.
        private void FlattenOneAccount(Account acc, Instrument instrument)
        {
            var pos = FindPosition(acc, instrument);
            if (pos == null || pos.Quantity == 0)
            {
                StatusUpdate?.Invoke(acc.Name + ": flat skip");
                return;
            }
            CancelQxBrackets(acc, instrument);   // B67 DW-B67-01: cancel before market order
            var action = pos.MarketPosition == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;
            try
            {
                acc.CreateOrder(
                    instrument, action, OrderType.Market, OrderEntry.Manual,
                    TimeInForce.Gtc, pos.Quantity, 0, 0, null, "PTT-Flatten",
                    DateTime.MaxValue, null);
                StatusUpdate?.Invoke(acc.Name + ": flatten " + pos.Quantity);
            }
            catch (Exception ex)
            {
                StatusUpdate?.Invoke("PTT-Flatten error: " + ex.Message);
            }
        }
```

---

## Section 4 — Files Changed

| File | Change Type | Lines Affected |
|---|---|---|
| `src/PropTraderTools/CopyEngine.cs` | MODIFY (comment replace + 1-line insert) | Lines 443–445 (comment), 1423–1424 (comment), insert after 1432 |
| `src/PropTraderTools/CopyEngineTests.cs` | ADD (4 new [Fact] methods) | Append to test class |

**No other files are touched.** Scope is strictly the minimum required to close DW-B67-01.

### Diff Size Estimate

- CopyEngine.cs: ~14 lines changed (2 comment replacements + 1 insert)
- CopyEngineTests.cs: ~55 lines added (4 test methods at ~12-14 lines each)
- Total: ~70 lines, well under the 10,000-character PR diff limit (JS-Code Review standard)

---

## Section 5 — CYC Analysis

### FlattenOneAccount BEFORE (CYC=3)

| # | Decision Point | Type |
|---|---|---|
| 1 | `if (pos == null \|\| pos.Quantity == 0)` | Guard / early return |
| 2 | `pos.MarketPosition == MarketPosition.Long ? ... : ...` | Ternary |
| 3 | `try/catch (Exception ex)` | Exception handler |

CYC = 3 (project comment convention: count of enumerated code segments).

### FlattenOneAccount AFTER (CYC=4)

| # | Code Segment | Type |
|---|---|---|
| 1 | `if (pos == null \|\| pos.Quantity == 0)` | Guard / early return |
| 2 | `CancelQxBrackets(acc, instrument)` | Method call (straight-line, enumerated per project convention) |
| 3 | `pos.MarketPosition == MarketPosition.Long ? ... : ...` | Ternary |
| 4 | `try/catch (Exception ex)` | Exception handler |

CYC = 4. **Still <= 8. PASS.**

Note: `CancelQxBrackets` is counted as segment (2) per project CYC comment convention (each
major code operation is enumerated). Under strict McCabe, the call itself is not a branch; the
strict McCabe value would remain 3. The comment reflects the project convention used consistently
across all B-blocks (see B28, B62, B65, B66-LaneB comments).

### CancelQxBrackets (unchanged)

CYC=6 (comment already correct). No change to logic. **PASS.**

---

## Section 6 — JS-DNA Compliance

### JS-001: No throw new Exception in hot path

| Location | Assessment |
|---|---|
| FlattenOneAccount `catch (Exception ex)` | Existing: catches and invokes StatusUpdate. No `throw` added. PASS |
| CancelQxBrackets `catch { }` | Existing: bare swallow. No `throw` added. PASS |
| New line `CancelQxBrackets(acc, instrument)` | Straight call, no throw. PASS |

**Result: JS-001 PASS — no violations introduced.**

### JS-002: No return null

Both methods are `void`. No return value, no null return possible.

**Result: JS-002 PASS — not applicable (void methods).**

### JS-021: No lock()

- No `lock()` statement in any new or modified code.
- `CancelQxBrackets` uses `acc.Cancel(stale.ToArray())` — NT8 API, no lock.
- `acc.CreateOrder` — NT8 API, no lock.
- Both methods run on the NT8 dispatcher thread (single-threaded NT8 event chain).

**Result: JS-021 PASS — zero lock() usage.**

### JS-036: No new byte[] heap allocation in hot path

- The single line added (`CancelQxBrackets(acc, instrument)`) introduces zero allocations.
- `CancelQxBrackets` itself allocates `new List<Order>()` and calls `.ToArray()` — these are
  PRE-EXISTING allocations, not introduced by this block.

**Result: JS-036 PASS — no new heap allocations introduced by this change.**

### ASCII-Only Compliance

All string literals in modified/added code:
- `"Cancel ALL bracket orders first to prevent race conditions."` — ASCII
- `"Close operation failed. Operation timed out."` — ASCII
- `"B67 DW-B67-01: cancel before market order"` — ASCII
- `"flat skip"`, `"PTT-Flatten"`, `"flatten "`, `"PTT-Flatten error: "` — existing, ASCII

**Result: ASCII PASS — no Unicode, no curly quotes, no emoji.**

### DateTime.Now Ban

`FlattenOneAccount` uses `DateTime.MaxValue` (not `DateTime.Now`). No change to this argument.

**Result: DateTime PASS.**

---

## Section 7 — Test Strategy

All tests are xUnit `[Fact]` methods. No NUnit. No MSTest.
File: `src/PropTraderTools/CopyEngineTests.cs` (append to existing test class).
Test infrastructure: use the existing Account/Instrument/Order stub pattern from prior blocks
(B28, B62, B65). The `CancelQxBrackets` method is `internal`; tests require
`[assembly: InternalsVisibleTo("CopyEngineTests")]` — confirm this is already present from B28 T1.

---

### T_B67_01 — CancelQxBrackets_called_before_CreateOrder

**What it verifies**: When `FlattenOneAccount` is called for an account with a non-zero long
position, `CancelQxBrackets` is invoked (observable via captured call sequence) BEFORE
`acc.CreateOrder` is called.

**Setup**:
- Stub `FindPosition` to return a non-null position with `Quantity=1`, `MarketPosition=Long`.
- Instrument the test CopyEngine subclass to record call order:
  - Override/wrap `CancelQxBrackets` to append `"CancelQxBrackets"` to a `List<string> callLog`.
  - Stub `acc.CreateOrder` call (or use null-safe wrapper) to append `"CreateOrder"` to `callLog`.

**Assert**:
```csharp
Assert.Equal("CancelQxBrackets", callLog[0]);
Assert.Equal("CreateOrder", callLog[1]);
```

---

### T_B67_02 — FlattenOneAccount_flat_position_noOp

**What it verifies**: When `FindPosition` returns null (or a position with `Quantity == 0`),
neither `CancelQxBrackets` nor `acc.CreateOrder` is called (early return guard respected).

**Setup**:
- Stub `FindPosition` to return `null`.
- Record whether `CancelQxBrackets` was called (call counter = 0).

**Assert**:
```csharp
Assert.Equal(0, cancelCallCount);
Assert.Equal(0, createOrderCallCount);
```

---

### T_B67_03 — FlattenOneAccount_long_position_produces_Sell_Market

**What it verifies**: When position is `MarketPosition.Long`, the `OrderAction` passed to
`acc.CreateOrder` is `OrderAction.Sell` and `OrderType` is `OrderType.Market`.

**Setup**:
- Stub `FindPosition` to return `Quantity=2`, `MarketPosition=Long`.
- Capture arguments to `acc.CreateOrder`.

**Assert**:
```csharp
Assert.Equal(OrderAction.Sell, capturedAction);
Assert.Equal(OrderType.Market, capturedOrderType);
```

---

### T_B67_04 — FlattenOneAccount_short_position_produces_BuyToCover_Market

**What it verifies**: When position is `MarketPosition.Short`, the `OrderAction` passed to
`acc.CreateOrder` is `OrderAction.BuyToCover` and `OrderType` is `OrderType.Market`.

**Setup**:
- Stub `FindPosition` to return `Quantity=1`, `MarketPosition=Short`.
- Capture arguments to `acc.CreateOrder`.

**Assert**:
```csharp
Assert.Equal(OrderAction.BuyToCover, capturedAction);
Assert.Equal(OrderType.Market, capturedOrderType);
```

---

## Section 8 — Scan Checklist (7 Scans — Engineer Contract)

Each scan MUST be executed by the engineer before marking the ticket complete.
A failing scan = ticket is NOT done. All 7 must PASS.

| Scan | ID | Command / Check | Pass Condition |
|---|---|---|---|
| Lock scan | S1 | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | Zero matches in new/modified lines |
| Throw new scan | S2 | `grep -n "throw new" src/PropTraderTools/CopyEngine.cs` | Zero matches in new/modified lines |
| CYC scan | S3 | Manual review: count branches in updated `FlattenOneAccount` | CYC=4 (4 segments per comment), confirm comment text matches |
| ASCII scan | S4 | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` | Zero non-ASCII characters in file |
| Build scan | S5 | `powershell -File .\scripts\build_readiness.ps1` | Zero errors, zero warnings on changed files |
| Test scan | S6 | `dotnet test src/PropTraderTools/CopyEngineTests.csproj --filter "T_B67"` | All 4 T_B67_* tests pass (green) |
| SHA-256 scan | S7 | `certutil -hashfile src/PropTraderTools/CopyEngine.cs SHA256` | Hash recorded in ticket-1-completion.md; confirms no unintended edits |

---

## Section 9 — Deferred Work

### DW-B67-01 — CLOSED this block

| Item | Resolution |
|---|---|
| DW-B67-01 (P0): FlattenOneAccount cancel-before-flatten | **CLOSED — implemented in B67-LaneA T1** |

### Carry-Forward Items (unchanged — no action in this block)

| ID | Priority | Description |
|---|---|---|
| DW-B67-02 | P0 | Open in B67-LaneB (parallel lane — not this lane) |
| DW-B66-C-02 | P1 | DispatchCopy dedup key issue |
| DW-B66-BE-01 | P1 | CancelQxBrackets PTT-BE-Stop on Quick Exit |
| DW-B63-01 | P1 | Spurious PTT-Copy brackets on Sim102 |
| DW-B54-01 | P1 | ATM auto-inject blocked |
| DW-B58-01 | P2 | Carry forward |
| DW-B58-02 | P2 | Carry forward |
| DW-B58-03 | P2 | Carry forward |
| PRE-EXISTING-01 | P2 | Carry forward |
| PRE-EXISTING-02 | P2 | Carry forward |
| PRE-EXISTING-03 | P2 | Carry forward |

---

*Plan status: REVIEW_PENDING — awaiting ptt-plan-reviewer.*
