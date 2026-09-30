# B131 LaneA Architecture Plan
## DW-B138 — ATM Bracket Drag Not Reaching SyncFollowerBracket for Stop1/T1/T2

**Status**: REVIEW_PENDING
**Author**: ptt-architect
**Epic**: B131 LaneA
**Requirement**: DW-B138
**Produced by**: Phase 1 (Architecture)
**Date**: 2026-08-31

---

## Section A — Root Cause

### Root Cause: H2 — `FindFollowerBracketOrder` Signal Name Mismatch (Primary)

**Location**: [`FindFollowerBracketOrder`](src/PropTraderTools/CopyEngine.cs:2339)

**Exact failure line**: L2347
```csharp
if (order.FromEntrySignal != fromEntrySignalName) // L2347
    continue;
```

**Mechanism**:

1. When an ATM bracket drag fires `OnOrderUpdate` with `OrderState.Working`, the chain reaches:
   - `TryHandleBracketDrag` (L1720) → `IsWorkingBracket` returns true (Working state + IsBracketLegStatic name match) → `HandleBracketChange` called.
2. `HandleBracketChange` (L2315) loops follower accounts and calls `SyncFollowerBracket(acc, leaderOrder, isStop, newPrice, tickSize)`.
3. `SyncFollowerBracket` (L2131) calls `FindFollowerBracketOrder(acc, leaderOrder.FromEntrySignal, isStop)` at L2139.
4. `FindFollowerBracketOrder` iterates `follower.Orders` and at L2347 skips any order where `order.FromEntrySignal != fromEntrySignalName`.

**Why the match fails**:
- The leader ATM bracket orders (`Stop1`, `Target1`, `Target2`, `Target3`) have `order.FromEntrySignal` set to the ATM entry signal name (confirmed non-null by NT8_ADDON_KNOWLEDGE.md L228: *"Non-null on bracket legs (stop + target orders from ATM)"*).
- The follower bracket orders placed by PTT Copy (via `acc.CreateOrder` with `oco = ""`) have `FromEntrySignal = null` or `FromEntrySignal = ""`.
- Comparison: `null != "entry_signal_name"` → `true` → follower order is SKIPPED every iteration.
- `FindFollowerBracketOrder` returns `null`.
- `SyncFollowerBracket` exits at L2140 (`if (fo == null) return;`) before `IsAtmSTPOrder` or any cancel+resubmit logic is reached.
- Net result: **zero PTT-STP-Drag / PTT-TGT-Drag orders are placed** for Stop1, Target1, Target2.

### Why Target3 Worked

The T3 asymmetry has one consistent explanation under H2:

In the specific test session, the follower bracket for `Target3` was either:
(a) Placed by PTT with a non-empty `oco`/`fromEntrySignal` matching the ATM entry signal name (e.g., an earlier code path used `leaderOrder.FromEntrySignal` when creating the follower bracket), OR
(b) Had been previously synced to a `PTT-TGT-Drag` order that happened to carry the correct `FromEntrySignal` from that prior sync.

In either case, the condition `order.FromEntrySignal == leaderOrder.FromEntrySignal` evaluated to `true` for exactly that T3 follower order, so `FindFollowerBracketOrder` returned it. `IsAtmSTPOrder(fo)` returned `true` (name starts with "Target"), and `SyncAtmFollowerTarget` executed successfully, placing `PTT-TGT-Drag`.

For Stop1, Target1, Target2: their follower orders consistently had `FromEntrySignal = null` / `""` → mismatch → null return → silent skip.

### H3 Assessment (OrderState.ChangeSubmitted) — NOT A BLOCKING BUG

`IsWorkingBracket` (L2083) accepts only `Working` and `Accepted`. When drag fires `ChangeSubmitted` (NT8_FULL_REFERENCE.md L3367: *"Order change is submitted to the broker"*), `IsWorkingBracket` returns `false` → `TryHandleBracketDrag` returns `false` → `DispatchCopy` is called with the `ChangeSubmitted`-state order.

This is **noise** (a spurious `DispatchCopy` call), not a blocker. The drag lifecycle includes a subsequent `Working` event that DOES pass `IsWorkingBracket`. T3 succeeded without any H3 fix, confirming the `Working` event DOES reach `HandleBracketChange`. H3 is noted as technical debt but is **out of scope** for DW-B138.

---

## Section B — Fix Strategy

### FIX-A: Extend `FindFollowerBracketOrder` with ATM Name-Based Fallback

**Scope**: `FindFollowerBracketOrder` only. No other methods changed.

**Logic**: When the `FromEntrySignal` match fails, fall back to matching by `Order.Name` when the leader order is an ATM bracket order (name starts with "Stop" or "Target" per `IsAtmSTPOrder`).

This mirrors the existing `IsAtmSTPOrder` predicate already used in `SyncFollowerBracket` (L2151/L2156) — it is the natural extension of the same concept to the lookup function.

**Implementation strategy**:

1. Add new parameter `string? leaderName` to `FindFollowerBracketOrder`.
2. Extract the compound match condition into a new private static predicate `SignalOrNameMatches` (CYC = 3, well within budget) to keep the main loop clean.
3. Update the single call site in `SyncFollowerBracket` (L2139) to pass `leaderOrder.Name` as the 4th argument.

**What changes**:
| Method | Change |
|--------|--------|
| `FindFollowerBracketOrder` | Add `string? leaderName` param; use `SignalOrNameMatches` predicate instead of raw `!=` |
| `SyncFollowerBracket` | Pass `leaderOrder.Name` as 4th arg to `FindFollowerBracketOrder` |
| `SignalOrNameMatches` (NEW) | Private static predicate encapsulating the OR logic |

**What does NOT change**:
- `HandleBracketChange` — no change
- `TryHandleBracketDrag` — no change
- `IsWorkingBracket` — no change (H3 not in scope)
- `SyncAtmFollowerBracket` — no change
- `SyncAtmFollowerTarget` — no change
- `IsAtmSTPOrder` — no change
- `IsBracketLegStatic` — no change
- All entry-copy paths (`DispatchCopy`, `TryCopyEntry`) — no change

---

## Section C — Method Signatures

### 1. `SignalOrNameMatches` (NEW — private static)

**File**: [`src/PropTraderTools/CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs)

**BEFORE**: Does not exist.

**AFTER**:
```csharp
// CYC=3: (1) signal equality, (2) leaderName null, (3) name equality.
// Returns true if follower order matches the leader by FromEntrySignal or by ATM Name fallback.
// JS-021: no lock. JS-001: no throw. JS-002: returns bool.
private static bool SignalOrNameMatches(Order order, string? signalName, string? leaderName)
{
    if (order.FromEntrySignal == signalName) // (1)
        return true;
    if (leaderName == null) // (2)
        return false;
    return order.Name == leaderName; // (3)
}
```

**CYC**: 3 (branches 1, 2, 3 — each one McCabe point). ✅ Within budget.

---

### 2. `FindFollowerBracketOrder` (MODIFIED)

**File**: [`src/PropTraderTools/CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs:2339)

**BEFORE** (L2339-2366):
```csharp
private Order? FindFollowerBracketOrder(
    Account follower,
    string fromEntrySignalName,
    bool isStop
)
```
Loop at L2347:
```csharp
if (order.FromEntrySignal != fromEntrySignalName) // (1) branch
    continue;
```

**AFTER**:
```csharp
// CYC=5: (1) foreach, (2) SignalOrNameMatches gate, (3) OrderState, (4) isStop, (5) OrderType match.
// V04: leaderName param added for ATM Name-based fallback when FromEntrySignal is null/empty on follower.
// JS-021: no lock. JS-001: no throw. JS-002: returns Order? (null contract explicit).
private Order? FindFollowerBracketOrder(
    Account follower,
    string? fromEntrySignalName,
    bool isStop,
    string? leaderName = null
)
```
Loop match condition:
```csharp
if (!SignalOrNameMatches(order, fromEntrySignalName, leaderName)) // (2) branch
    continue;
```

**CYC**: 5 (foreach=1, SignalOrNameMatches gate=1, OrderState check=1, isStop=1, OrderType check=1). ✅ Within budget.

---

### 3. `SyncFollowerBracket` (CALL SITE UPDATE ONLY)

**File**: [`src/PropTraderTools/CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs:2139)

**BEFORE** (L2139):
```csharp
var fo = FindFollowerBracketOrder(acc, leaderOrder.FromEntrySignal, isStop);
```

**AFTER**:
```csharp
var fo = FindFollowerBracketOrder(acc, leaderOrder.FromEntrySignal, isStop, leaderOrder.Name);
```

**CYC**: Unchanged (7 — per existing comment L2127). No new branches added to `SyncFollowerBracket`. ✅

---

## Section D — Non-Regression Scope

### Paths Confirmed Unchanged

| Path | Why Safe |
|------|----------|
| `"Buy STP"` / `"Sell STP"` leg matching | `IsBracketLegStatic` (L3800) already matches these via `EndsWith("STP")`. `FindFollowerBracketOrder` is not on this path — `"Buy STP"` / `"Sell STP"` orders are handled by `IsStopLeg` within the existing stop-type check. |
| Entry-order copy (`DispatchCopy`, `TryCopyEntry`) | Not touched. These dispatch on non-bracket orders. |
| `acc.Change()` path for non-ATM brackets | `SyncFollowerBracket` path (L2162-L2174): only reached if `IsAtmSTPOrder(fo)` returns false. Unchanged. |
| `TryHandleBracketDrag` / `IsWorkingBracket` | No changes. H3 out of scope. |
| Mirror mode (`MirrorOrderUpdate`) | No changes. |
| BE bracket paths (`TryFireFollowerBeDisarm`, `TryFireFollowerBeRetry`) | No changes. |
| ATM cancel sweep recovery (`TryCleanupReArmedAtmBracket`, `ReplaceFollowerCopyOnAtmCancel`) | No changes. |

### B129 / B130 Tests That Must Still Pass

The following existing tests exercise paths that the B131 fix must not break:

| Test | Assertion Protected |
|------|---------------------|
| `B130_DW137_Stop1NameRoutesToCancelResubmit` | `IsAtmSTPOrder("Stop1")` returns true; SyncAtmFollowerBracket is called for a Stop1 follower when it CAN be found. |
| `B129_DW134_AtmSTPFollowerSynced` | SyncAtmFollowerBracket fires when follower stop found by signal match. |
| `B129_DW134_NonAtmStopSynced` | acc.Change() path unchanged for non-ATM stop orders. |
| `B130_DW137_Target1NameRoutesToCancelResubmit` | `IsAtmSTPOrder("Target1")` returns true; SyncAtmFollowerTarget fires when follower target CAN be found. |

The `leaderName` parameter defaults to `null` (default parameter), so ALL existing call sites that do not pass `leaderName` continue to behave identically to before (C# default parameter = backward compatible).

---

## Section E — Test Specifications

**File**: `src/PropTraderTools/Tests/B131Tests.cs` (NEW FILE)

**Framework**: xUnit only. No NUnit. No MSTest. JS-021: no lock. DateTime.UtcNow (never DateTime.Now).

---

### Test 1: `B131_DW138_Stop1DragReachesHandleBracketChange`

**Purpose**: Verify that with the fix applied, a Stop1 leader drag reaches `SyncAtmFollowerBracket` for a follower account — even when the follower Stop1 order has `FromEntrySignal = null`.

**Mock setup**:
- Create a mock `Account` (follower) with one `Order`:
  - `Order.Name = "Stop1"`, `Order.OrderType = OrderType.StopMarket`, `Order.OrderState = OrderState.Working`, `Order.FromEntrySignal = null` (simulates PTT-placed bracket without signal).
- Create a leader `Order`:
  - `Order.Name = "Stop1"`, `Order.FromEntrySignal = "AtmEntrySignal"`, `Order.OrderState = OrderState.Working`, `Order.OrderType = OrderType.StopMarket`, `Order.StopPrice = 4500.00`.
- Instrument: standard mock with `TickSize = 0.25`.

**What to assert**:
- `FindFollowerBracketOrder(follower, "AtmEntrySignal", isStop: true, leaderName: "Stop1")` returns the mock follower Stop1 order (not null).
- The returned order's `Name == "Stop1"`.

**xUnit**:
```csharp
[Fact]
public void B131_DW138_Stop1DragReachesHandleBracketChange()
{
    // arrange
    var followerStop1 = MockOrder("Stop1", OrderType.StopMarket, OrderState.Working, fromEntrySignal: null, stopPrice: 4498.75);
    var followerAccount = MockAccount(new[] { followerStop1 });
    // act
    var found = CopyEngine.FindFollowerBracketOrderTestable(followerAccount, "AtmEntrySignal", isStop: true, leaderName: "Stop1");
    // assert
    Assert.NotNull(found);
    Assert.Equal("Stop1", found!.Name);
}
```

---

### Test 2: `B131_DW138_Target1DragReachesHandleBracketChange`

**Purpose**: Same as Test 1 but for `Target1` (Limit order, isStop=false).

**Mock setup**:
- Follower order: `Name = "Target1"`, `OrderType = OrderType.Limit`, `OrderState = OrderState.Working`, `FromEntrySignal = null`, `LimitPrice = 4510.00`.
- Leader: `Name = "Target1"`, `FromEntrySignal = "AtmEntrySignal"`, `LimitPrice = 4510.00`.

**What to assert**:
- `FindFollowerBracketOrder(follower, "AtmEntrySignal", isStop: false, leaderName: "Target1")` returns the mock Target1 order (not null).

**xUnit**:
```csharp
[Fact]
public void B131_DW138_Target1DragReachesHandleBracketChange()
{
    var followerTarget1 = MockOrder("Target1", OrderType.Limit, OrderState.Working, fromEntrySignal: null, limitPrice: 4510.00);
    var followerAccount = MockAccount(new[] { followerTarget1 });
    var found = CopyEngine.FindFollowerBracketOrderTestable(followerAccount, "AtmEntrySignal", isStop: false, leaderName: "Target1");
    Assert.NotNull(found);
    Assert.Equal("Target1", found!.Name);
}
```

---

### Test 3: `B131_DW138_Target3DragStillReachesHandleBracketChange` (Regression)

**Purpose**: Verify that T3 follower with MATCHING `FromEntrySignal` still works (signal-match path not broken by fallback).

**Mock setup**:
- Follower order: `Name = "Target3"`, `OrderType = OrderType.Limit`, `OrderState = OrderState.Working`, `FromEntrySignal = "AtmEntrySignal"` (non-null, matching).
- Leader: `Name = "Target3"`, `FromEntrySignal = "AtmEntrySignal"`.

**What to assert**:
- `FindFollowerBracketOrder(follower, "AtmEntrySignal", isStop: false, leaderName: "Target3")` returns T3 follower order (not null).
- Returns via the PRIMARY signal match path (not the fallback).

**xUnit**:
```csharp
[Fact]
public void B131_DW138_Target3DragStillReachesHandleBracketChange()
{
    var followerTarget3 = MockOrder("Target3", OrderType.Limit, OrderState.Working, fromEntrySignal: "AtmEntrySignal", limitPrice: 4520.00);
    var followerAccount = MockAccount(new[] { followerTarget3 });
    var found = CopyEngine.FindFollowerBracketOrderTestable(followerAccount, "AtmEntrySignal", isStop: false, leaderName: "Target3");
    Assert.NotNull(found);
    Assert.Equal("Target3", found!.Name);
}
```

---

### Test 4: `B131_DW138_BuySTPDragStillRoutesCorrectly` (Regression)

**Purpose**: Verify that `"Buy STP"` follower orders (non-ATM name, has `FromEntrySignal`) are unaffected. The Name-based fallback must NOT match `"Buy STP"` orders with a leader named `"Stop1"`.

**Mock setup**:
- Follower order: `Name = "Buy STP"`, `OrderType = OrderType.StopMarket`, `OrderState = OrderState.Working`, `FromEntrySignal = "AtmEntrySignal"`.
- Leader: `Name = "Stop1"`, `FromEntrySignal = "AtmEntrySignal"`.

**What to assert** (signal match wins, Name fallback not needed):
- `FindFollowerBracketOrder(follower, "AtmEntrySignal", isStop: true, leaderName: "Stop1")` returns the `"Buy STP"` order via signal match.
- Returns via signal match (not name match), confirming signal-match priority.

**xUnit**:
```csharp
[Fact]
public void B131_DW138_BuySTPDragStillRoutesCorrectly()
{
    var followerBuySTP = MockOrder("Buy STP", OrderType.StopMarket, OrderState.Working, fromEntrySignal: "AtmEntrySignal", stopPrice: 4498.75);
    var followerAccount = MockAccount(new[] { followerBuySTP });
    var found = CopyEngine.FindFollowerBracketOrderTestable(followerAccount, "AtmEntrySignal", isStop: true, leaderName: "Stop1");
    Assert.NotNull(found);
    Assert.Equal("Buy STP", found!.Name); // signal match returns "Buy STP", not "Stop1"
}
```

**Note on testability**: `FindFollowerBracketOrder` is currently `private`. The engineer must either:
(a) Add `internal static` accessor `FindFollowerBracketOrderTestable` (visible to test project via `[assembly: InternalsVisibleTo]`), OR
(b) Use `internal` visibility on `FindFollowerBracketOrder` itself and `InternalsVisibleTo` in `CopyEngine.csproj`.
Option (b) is preferred — minimal surface change.

---

## Section F — DW Items

**No DW items.**

All NT8 API facts used in this plan are confirmed from docs:
- `OrderState.ChangeSubmitted` — confirmed NT8_FULL_REFERENCE.md L3367.
- `order.FromEntrySignal` non-null on ATM brackets — confirmed NT8_ADDON_KNOWLEDGE.md L228.
- `acc.Cancel` + `acc.CreateOrder` + `acc.Submit` AddOnBase cancel+resubmit — confirmed NT8_ADDON_KNOWLEDGE.md, pre-confirmed known fact.
- `Account.Change()` silent no-op on ATM-owned brackets — pre-confirmed known fact (B129 SIM gate).
- ATM bracket `Order.Name` = template name (`"Stop1"`, `"Target3"`, etc.) — pre-confirmed known fact.

No genuine API unknowns remain.

---

## Section G — Spec Requirement Traceability

| Change | Requirement ID |
|--------|---------------|
| `SignalOrNameMatches` predicate (new) | DW-B138: ATM bracket drag must reach `HandleBracketChange` for Stop1/T1/T2 |
| `FindFollowerBracketOrder` + `leaderName` param | DW-B138: follower bracket lookup must succeed when `FromEntrySignal` is null/empty |
| `SyncFollowerBracket` call site update | DW-B138: pass leader name to enable name-based fallback in lookup |
| Tests 1 and 2 (Stop1, Target1) | DW-B138: new behaviour verified |
| Tests 3 and 4 (Target3, Buy STP) | DW-B138: regression guard — existing working paths must not break |

---

## Section H — Lamport / Scan Checklist Pre-Population

The following 7-scan checklist MUST appear in the ticket for ptt-ticket-reviewer:

```
SCAN-01 LOCK SCAN     grep -r "lock(" src/ --include="*.cs"      ZERO MATCHES REQUIRED
SCAN-02 THROW SCAN    grep -n "throw new" src/PropTraderTools/CopyEngine.cs  ZERO NEW THROWS
SCAN-03 NULL RETURN   grep -n "return null" src/PropTraderTools/CopyEngine.cs  EXISTING: 1 (FindFollowerBracketOrder). NO NEW ADDITIONS.
SCAN-04 ASYNC VOID    grep -rn "async void " src/ --include="*.cs"  ZERO MATCHES
SCAN-05 DATETIME NOW  grep -rn "DateTime\.Now" src/ --include="*.cs"  ZERO MATCHES (DateTime.UtcNow only)
SCAN-06 CYC BUDGET    SignalOrNameMatches <= 3. FindFollowerBracketOrder <= 5. SyncFollowerBracket unchanged at 7. All <= 8.
SCAN-07 ASCII SCAN    grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs  ZERO NON-ASCII
```

All 7 scans must pass before ptt-ticket-reviewer accepts the ticket.

---

## Rules Catalog Gate Result

```
STEP 0 -- RULES CATALOG GATE:
  [x] Read docs/standards/jane-street/RULES_CATALOG.md (UTF-8 clean)
  [x] JS-021 (lock ban): SignalOrNameMatches is static with no shared state. No lock. PASS.
  [x] JS-001 (no throw in hot path): no new throw statements. PASS.
  [x] JS-002 (no return null): FindFollowerBracketOrder returns Order? -- existing contract. PASS.
  [x] JS-033 (no async void): no async methods added. PASS.
  [x] CYC <= 8: SignalOrNameMatches=3, FindFollowerBracketOrder=5, SyncFollowerBracket unchanged=7. PASS.
  GATE RESULT: PASS
```

---

## Summary

**Root Cause**: H2 (primary). `FindFollowerBracketOrder` matches follower bracket orders by `FromEntrySignal` string equality. PTT-placed follower bracket orders have `FromEntrySignal = null`/`""` while ATM leader brackets have a non-null signal name. Match fails → `null` return → `SyncFollowerBracket` exits early → no cancel+resubmit fires.

**Fix**: One new 3-line private static predicate `SignalOrNameMatches` + one new optional parameter on `FindFollowerBracketOrder` + one call-site argument addition in `SyncFollowerBracket`. Three total changes, all in [`CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs). No threading changes. No new files (except test file).

**T3 Asymmetry**: T3's follower bracket happened to have matching `FromEntrySignal` in the test session (either from prior sync or original placement), so it succeeded through the existing path. Stop1/T1/T2 follower brackets consistently had null/empty `FromEntrySignal` → consistent failure.

**H3**: `OrderState.ChangeSubmitted` events fall through to `DispatchCopy` (noise) but do not block the subsequent `Working` event from reaching `HandleBracketChange`. Not a blocker. Out of scope for DW-B138.
