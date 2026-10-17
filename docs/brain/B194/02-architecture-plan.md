# B194 Architecture Plan

**Epic**: B194 -- Fix 4 confirmed NakedPositionDetector + DrainThenDispatch bugs
**Author**: ptt-architect (Phase 1)
**Date**: 2026-10-07
**Status**: PLAN_COMPLETE (pending ptt-plan-reviewer Phase 2)
**Preceding backlog**: docs/brain/B193/06-deferred-backlog.md
**Output file**: docs/brain/B194/04-tickets.md (Phase 3)

---

## 1. Epic Summary

B194 delivers 4 surgical fixes to `src/PropTraderTools/CopyEngine.cs` addressing confirmed P0/P1
root causes in the NakedPositionDetector subsystem and the DrainThenDispatch drain-race path.
No other files are modified. All fixes are in-place surgical edits to existing methods plus one
new private helper method and one new ConcurrentDictionary field.

---

## 2. LANE-SPLIT GATE

**Q1. Are all fixes in the same method or within 50 lines?**
NO. The 4 fixes span 4 distinct methods across ~1600 lines of CopyEngine.cs:
- TryNakedDetect (~line 6450)
- HasNakedPosition (~line 6501) + new IsBracketStopOrder helper (~line 6499)
- FlattenOneAccount (~line 5004)
- DrainThenDispatch (~line 6541)

**Q2. Does Fix B design depend on Fix A final design?**
NO. Each fix addresses an independent root cause:
- F1 (TryNakedDetect): guards against PTT-Copy fill entering NakedDetect pipeline
- F2 (HasNakedPosition): corrects TriggerPending stop recognition inside the pipeline
- F3 (DrainThenDispatch): fixes Submitted exclusion in drain candidate filter
- F4 (FlattenOneAccount): belt-and-suspenders debounce on flatten submission
No fix's code depends on any other fix's implementation.

**Q3. Does each fix have standalone value if the other is blocked?**
YES.
- F1 alone: eliminates BUG-1 false positives even if F2 is blocked
- F2 alone: corrects stop recognition even without F1 in place
- F3 alone: fixes drain race completely independently
- F4 alone: prevents flatten loops even without F1

**Q4. Does each fix have an independent SIM verification path?**
YES.
- F1: observe followers remain alive after PTT-Copy fill; no PTT-Flatten:Submitted in log
- F2: simulate TriggerPending ATM stop; verify HasNakedPosition returns false
- F3: observe acct=Sim104 appears in [DRAIN] log on drag-3; verify no double PTT-Copy
- F4: trigger two FlattenOneAccount calls within 500ms for same account; second skips

**LANE-SPLIT GATE RESULT: SINGLE-PIPELINE**

Rationale: All 4 fixes target a single file. Sequential pipeline (T1 -> T2 -> T3) is optimal.
T1 groups F1+F3 (both are minimal single-location insertions with zero interdependency).
T2 addresses F2 (medium-complexity inner loop refactor with helper extraction).
T3 addresses F4 (new field + debounce pattern, matching existing _nakedDetectLastQueuedTicks).

---

## 3. Confirmed Root Causes (Read-Only Reference)

| Bug | Priority | Method | Root Cause Summary |
|-----|----------|--------|--------------------|
| BUG-1 | P0 | TryNakedDetect | PTT-Copy fill triggers NakedDetect before brackets arrive |
| BUG-2 | P1 | HasNakedPosition | TriggerPending+Market stops not recognized as bracket stops |
| BUG-3 | P0 | FlattenOneAccount | Infinite flatten loop from BUG-1 source; residual PTT-Flatten fill path |
| BUG-4 | P0 | DrainThenDispatch | Submitted orders excluded from entryCandidates drain filter |

---

## 4. Fix Designs

### F1 -- TryNakedDetect PTT-Copy Fill Guard

**Bug closes**: DW-B194-01
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `TryNakedDetect` (~line 6450)

**Problem**: When a follower's PTT-Copy order fills, TryNakedDetect fires (state=Filled,
follower=true). HasNakedPosition returns true because the follower has a position but brackets
have not yet arrived (brackets sync after leader's Target1:Accepted, which fires after the fill).
This immediately queues FlattenOneAccount, closing the follower before brackets land.

**Fix**: Add an early return guard at the top of TryNakedDetect (before all existing checks)
that identifies PTT-Copy fills and exits immediately. PTT-Copy fills are the copy engine's own
synthetic entry orders; they should never trigger naked detection because the bracket gap is
structural and expected.

**Code insertion point**: After the method comment block, before the first `if (e.Order.OrderState...)`:

```csharp
// B194-F1: PTT-Copy fill guard -- follower fill has no brackets yet; brackets arrive
// after leader Target1:Accepted. Never trigger NakedPositionDetector on own copy fill.
// DW-B194-01: closed by this guard.
if (e.Order.Name == "PTT-Copy" && e.Order.OrderState == OrderState.Filled)
    return;
```

**CYC analysis**:
- Pre-F1 (team counting): CYC = 3 (terminal-state compound check = 1, follower-account check = 1, NakedPositionDetector call = 1)
- Post-F1: CYC = 4 (new PTT-Copy fill guard = +1)
- JS-066 limit: 8. PASSES. ✓

---

### F2 -- HasNakedPosition TriggerPending Stop Recognition

**Bug closes**: DW-B194-02
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `HasNakedPosition` (~line 6501) + new `IsBracketStopOrder` (~line 6499)

**Problem**: NT8 SIM ATM stops appear as `OrderType.Market + OrderState.TriggerPending + StopPrice > 0`.
The existing inner loop filter only recognizes `Working|Submitted` state combined with
`StopMarket|StopLimit` types. TriggerPending Market stops are invisible to the filter, causing
HasNakedPosition to return true even when a bracket stop exists.

**CYC pre-check on HasNakedPosition (current)**:
Using team's simplified counting (compound predicate = 1 decision):
1. foreach (Position p) = +1
2. if (p.Quantity > 0) = +1
3. if (!hasPosition) return false = +1
4. foreach (Order o) = +1
5. if (o.OrderState != Working && != Submitted) continue = +1
6. if (o.OrderType == StopMarket || StopLimit) = +1
7. else if (o.OrderType == Limit) = +1
Base = 1. **CYC = 8** (at JS-066 ceiling).

Adding F2 directly to HasNakedPosition without extraction would push CYC to 9. **VIOLATION.**

**Solution**: Extract `IsBracketStopOrder(Order o)` as a new `private static bool` helper.
The helper consolidates all stop-order recognition (TriggerPending + live stops) into CYC = 3.
HasNakedPosition's inner loop is simplified to use the helper, reducing its CYC to 7.

**New method IsBracketStopOrder**:

```csharp
// B194-F2: recognize all bracket stop patterns.
// (1) NT8 SIM ATM stops: TriggerPending + Market + StopPrice>0.
// (2) Live stops: Working|Submitted + StopMarket|StopLimit.
// CYC=3: base(1) + TriggerPending-branch(1) + WorkingStop-branch(1).
// JS-021: no lock. JS-001: no throw. NT8: o.StopPrice confirmed via B194-DIRECT commit.
// DW-B194-02: closed by this helper.
private static bool IsBracketStopOrder(Order o)
{
    if (o.OrderState == OrderState.TriggerPending
        && o.OrderType == OrderType.Market
        && o.StopPrice > 0)
        return true;
    if ((o.OrderState == OrderState.Working || o.OrderState == OrderState.Submitted)
        && (o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit))
        return true;
    return false;
}
```

**HasNakedPosition inner loop replacement**:

Remove these lines from the `foreach (Order o in acct.Orders)` body:
```csharp
// REMOVE:
if (o.OrderState != OrderState.Working && o.OrderState != OrderState.Submitted)
    continue;
if (o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit)
    hasStop = true;
else if (o.OrderType == OrderType.Limit)
    hasTarget = true;
```

Replace with:
```csharp
// REPLACE WITH:
if (IsBracketStopOrder(o)) { hasStop = true; continue; }
if ((o.OrderState == OrderState.Working || o.OrderState == OrderState.Submitted)
    && o.OrderType == OrderType.Limit)
    hasTarget = true;
```

**CYC analysis post-F2**:
HasNakedPosition:
1. foreach (Position p) = +1
2. if (p.Quantity > 0) = +1
3. if (!hasPosition) return false = +1
4. foreach (Order o) = +1
5. if (IsBracketStopOrder(o)) = +1
6. if (Working|Submitted && Limit) = +1
Base = 1. **CYC = 7** (improved from 8). ✓

IsBracketStopOrder:
1. if (TriggerPending && Market && StopPrice>0) = +1
2. if (Working|Submitted && StopMarket|StopLimit) = +1
Base = 1. **CYC = 3**. ✓

JS-066 limit: 8. BOTH PASS. ✓

**Insertion point for IsBracketStopOrder**: Place immediately before `HasNakedPosition` method
(currently at ~line 6501). After insertion, IsBracketStopOrder occupies ~lines 6499-6515,
pushing HasNakedPosition to ~line 6517.

---

### F3 -- DrainThenDispatch Submitted Order Inclusion

**Bug closes**: DW-B194-03
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `DrainThenDispatch` (~line 6541)

**Problem**: When drain-2 completes for Sim104 (SubmitDrainedEntry), the new PTT-Copy enters
Submitted state. When drag-3 fires, `entryCandidates` filter excludes Submitted orders
(`Working|Accepted` only), so `entryCandidates.Any() = false`. This triggers `SubmitEntryDirect`
at drag-3 price while the old Submitted PTT-Copy (drag-2 price) is still live, resulting in
2 simultaneous PTT-Copy orders for Sim104.

**Fix**: Extend the `OrderState` filter in the `entryCandidates` LINQ predicate to include
`OrderState.Submitted`.

**Code change** at ~line 6558:

```csharp
// BEFORE:
&& (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted)

// AFTER (B194-F3: include Submitted -- PTT-Copy from completed drain-2 may be Submitted):
&& (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted
    || o.OrderState == OrderState.Submitted)
```

**CYC analysis**: This change is entirely within a LINQ `Where()` predicate (lambda expression).
The method body's McCabe CYC is unchanged. **CYC = no change**. ✓

---

### F4 -- FlattenOneAccount Belt-and-Suspenders Debounce

**Bug closes**: DW-B194-04
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `FlattenOneAccount` (~line 5004)
**New field**: `_flattenFilledTicks` (~line 376)

**Architect Decision -- IMPLEMENT F4**:

F1 eliminates BUG-3's primary path (PTT-Copy fill triggering NakedDetect). However, F1 does NOT
cover the residual path where a PTT-Flatten fill itself re-triggers TryNakedDetect:
- PTT-Flatten fills: Name="PTT-Flatten", State=Filled, follower=true
- TryNakedDetect's F1 guard only filters Name=="PTT-Copy" -- PTT-Flatten fills pass the guard
- If the flatten reversed the position (short after long or vice versa), HasNakedPosition=true
- This queues another FlattenOneAccount -- potential loop at >500ms intervals

Evidence from BUG-3: 9 reversals in 7 seconds (~0.78s intervals), just outside the existing
500ms _nakedDetectLastQueuedTicks debounce window. F4 adds a parallel debounce at the
FlattenOneAccount submission layer, providing defense-in-depth.

**New field** (after `_nakedDetectLastQueuedTicks` at ~line 374):

```csharp
// B194-F4: belt-and-suspenders flatten debounce.
// Stores (long)Environment.TickCount at last acc.Submit(PTT-Flatten) per account name.
// Prevents rapid flatten re-submissions from PTT-Flatten fill triggering NakedDetect residual path.
// ConcurrentDictionary: no lock (JS-021). StringComparer.Ordinal: deterministic key comparison.
private readonly ConcurrentDictionary<string, long> _flattenFilledTicks =
    new ConcurrentDictionary<string, long>(StringComparer.Ordinal);
```

**FlattenOneAccount additions**:

Insertion 1 -- debounce check (after existing in-flight scan loop, before `FindPosition` call,
i.e., after ~line 5029 and before ~line 5030):

```csharp
// B194-F4: belt-and-suspenders debounce. Skip if flatten was submitted within 500ms.
// Prevents PTT-Flatten fill -> NakedDetect -> FlattenOneAccount re-entry loop.
long flattenNow = (long)(int)Environment.TickCount;
if (flattenNow - _flattenFilledTicks.GetOrAdd(acc.Name, 0L) < 500L)
{
    StatusUpdate?.Invoke(acc.Name + ": flat-debounce skip");
    return;
}
```

Insertion 2 -- timestamp recording (inside `try` block, after null check on `order`,
before `acc.Submit(new[] { order })`):

```csharp
_flattenFilledTicks.AddOrUpdate(acc.Name, flattenNow, (_, __) => flattenNow);
```

Note: `flattenNow` is declared in Insertion 1 and is in scope for Insertion 2 (same method body).
The timestamp is recorded at submission time, not at fill time. This ensures the 500ms lockout
begins from the moment the flatten is dispatched to NT8.

**CYC analysis post-F4**:
FlattenOneAccount existing CYC = 6 (per code comment at ~line 5000-5002).
Insertion 1 adds 1 branch (the debounce if). **Post-F4 CYC = 7**. ✓
JS-066 limit: 8. PASSES. ✓

---

## 5. McCabe CYC Summary Table

| Method | Pre-Fix CYC | Fix | Post-Fix CYC | JS-066 Limit | Status |
|--------|-------------|-----|--------------|--------------|--------|
| `TryNakedDetect` | 3 | F1 | 4 | 8 | PASS ✓ |
| `HasNakedPosition` | 8 | F2 | 7 | 8 | PASS ✓ |
| `IsBracketStopOrder` (NEW) | -- | F2 | 3 | 8 | PASS ✓ |
| `DrainThenDispatch` | unchanged | F3 | unchanged | 8 | PASS ✓ |
| `FlattenOneAccount` | 6 | F4 | 7 | 8 | PASS ✓ |

---

## 6. Component List

All changes are in a single file:

```
src/PropTraderTools/CopyEngine.cs
```

### New symbols added:
- `private static bool IsBracketStopOrder(Order o)` -- new helper method (~line 6499 after insertion)
- `private readonly ConcurrentDictionary<string, long> _flattenFilledTicks` -- new field (~line 376)

### Modified symbols:
- `private void TryNakedDetect(OrderEventArgs e)` (~line 6450) -- F1 guard
- `private static bool HasNakedPosition(Account acct)` (~line 6517 after insertion) -- F2 refactor
- `private void DrainThenDispatch(...)` (~line 6541) -- F3 LINQ extension
- `private void FlattenOneAccount(Account acc, Instrument instrument)` (~line 5004) -- F4 debounce

### No changes to:
- `TradeCopierPanel.cs`
- `PropTraderTools.Tests.csproj`
- Any other .cs files

---

## 7. Data Flow

### BUG-1/BUG-2 Fix Path (F1 + F2):

```
OnOrderUpdate (NT8 acct thread)
  -> TryNakedDetect(e)
     [F1 guard]: if Name=="PTT-Copy" && Filled -> return (BUG-1 closed)
     [existing]: if not terminal state -> return
     [existing]: if not follower account -> return
     -> NakedPositionDetector(acct)
        -> HasNakedPosition(acct)
           -> foreach Positions: check quantity
           -> foreach Orders: IsBracketStopOrder(o) [F2 new helper]
              -> TriggerPending+Market+StopPrice>0 -> true (BUG-2 closed)
              -> Working|Submitted+StopMarket|StopLimit -> true
           -> if stop found: return false (no flatten needed)
        if naked: Dispatcher.InvokeAsync(FlattenOneAccount)
```

### BUG-4 Fix Path (F3):

```
OnOrderUpdate (NT8 acct thread)
  -> DrainThenDispatch(follower, instrument, qty, price, action, orderType)
     entryCandidates = ActiveOrders(follower)
       .Where(o => ... && (Working || Accepted || Submitted) [F3: +Submitted] ...)
       .ToList()
     if entryCandidates.Any() -> cancel all -> PendingDispatchDrain
     else -> SubmitEntryDirect (only if no Submitted PTT-Copy in-flight)
```

### BUG-3 Residual Fix Path (F4):

```
Dispatcher.InvokeAsync callback (UI thread)
  -> FlattenOneAccount(acc, instrument)
     [existing]: scan acc.Orders for active PTT-Flatten (Submitted|Accepted|Working) -> skip if found
     [F4 new]: if _flattenFilledTicks[acc.Name] < 500ms ago -> StatusUpdate + return
     -> FindPosition
     -> TryMarkFollowerSlateNotReady
     -> CancelAllAccountOrders
     -> FindPosition (post-cancel)
     [F4 new]: _flattenFilledTicks.AddOrUpdate(acc.Name, flattenNow, ...)
     -> acc.Submit(PTT-Flatten)
```

---

## 8. Threading Model

All threading patterns match existing codebase conventions:

| Context | Thread | Access Pattern |
|---------|--------|---------------|
| TryNakedDetect (F1 guard) | NT8 account thread | Read e.Order.Name/State -- read-only, thread-safe |
| IsBracketStopOrder (F2) | NT8 account thread | Read o.OrderState/Type/StopPrice -- read-only, thread-safe |
| DrainThenDispatch (F3) | NT8 account thread | LINQ predicate -- no state mutation |
| FlattenOneAccount debounce check (F4) | UI thread (via Dispatcher.InvokeAsync) | ConcurrentDictionary.GetOrAdd -- lock-free |
| FlattenOneAccount timestamp record (F4) | UI thread | ConcurrentDictionary.AddOrUpdate -- lock-free |

JS-021 compliance: All 4 fixes use zero lock() statements. F4 uses ConcurrentDictionary
(approved lock-free primitive per lock-free-patterns.md). ✓

---

## 9. NT8 API Usage

All NT8 API calls in this epic are pre-confirmed and in current use:

| API | Availability | Evidence |
|-----|-------------|---------|
| `OrderState.TriggerPending` | AddOnBase confirmed | B194-DIRECT commit a434679a (SnapshotStopPrice fix) |
| `OrderType.Market` | Standard NT8 enum | In current use throughout CopyEngine.cs |
| `Order.StopPrice` | AddOnBase confirmed | B194-DIRECT commit a434679a |
| `Account.Cancel(Order[])` | AddOnBase confirmed | NT8_FULL_REFERENCE.md |
| `Account.CreateOrder()` | AddOnBase confirmed | NT8_FULL_REFERENCE.md |
| `Account.Submit()` | AddOnBase confirmed | NT8_FULL_REFERENCE.md |
| `Dispatcher.InvokeAsync()` | In current use | NakedPositionDetector line 6491 |
| `ConcurrentDictionary<string,long>` | Lock-free, in current use | _nakedDetectLastQueuedTicks line 373 |
| `Environment.TickCount` | In current use | NakedPositionDetector line 6479 |

No StrategyBase-only methods used. No AtmStrategyCreate, no AtmStrategyChangeStopTarget. ✓

---

## 10. Ticket Breakdown

### T1 -- Fix TryNakedDetect (F1) + Fix DrainThenDispatch (F3)

**Spec requirements**: BUG-1 (P0), BUG-4 (P0)
**DW cards closed**: DW-B194-01, DW-B194-03
**File**: `src/PropTraderTools/CopyEngine.cs`

**Changes**:
1. Insert F1 guard at top of `TryNakedDetect` (3 lines)
2. Add `|| o.OrderState == OrderState.Submitted` to `DrainThenDispatch` LINQ predicate (1 line)

**Method signatures** (unchanged, surgical insert only):
- `private void TryNakedDetect(OrderEventArgs e)` -- existing, modified
- `private void DrainThenDispatch(Account follower, Instrument instrument, int qty, double price, OrderAction action, OrderType orderType)` -- existing, modified

**Tests** (xUnit [Fact], file: `src/PropTraderTools/Tests/B194Tests.cs`):
- `TryNakedDetect_SkipsPTTCopyFill_NoNakedDetectorCalled`
  Assert: when order has Name="PTT-Copy" and State=Filled, NakedPositionDetector is NOT invoked.
  Mechanism: verify StatusUpdate event (used by NakedPositionDetector logging path) is not fired.
- `DrainThenDispatch_IncludesSubmittedInEntryCandidates`
  Assert: when PTT-Copy order is in Submitted state, it appears in entryCandidates and cancel is sent.
  Mechanism: supply a stub account with one Submitted PTT-Copy Limit order; verify Cancel is called.

**7-Scan Checklist (T1 engineer contract)**:
- SCAN-01 (JS-021 no lock): no lock() in TryNakedDetect or DrainThenDispatch after change ✓
- SCAN-02 (JS-033 no async void): no new async void methods ✓
- SCAN-03 (JS-066 CYC<=8): TryNakedDetect CYC=4, DrainThenDispatch unchanged ✓
- SCAN-04 (JS-001 no throw): no throw in F1 guard, no throw in F3 predicate ✓
- SCAN-05 (ASCII-only): "PTT-Copy" is ASCII; no new string literals ✓
- SCAN-06 (DateTime.UtcNow only): no DateTime changes ✓
- SCAN-07 (no FontFamily/hex colors): no UI changes ✓

---

### T2 -- Fix HasNakedPosition (F2) + Extract IsBracketStopOrder

**Spec requirements**: BUG-2 (P1)
**DW cards closed**: DW-B194-02
**File**: `src/PropTraderTools/CopyEngine.cs`

**Changes**:
1. Insert new `private static bool IsBracketStopOrder(Order o)` method (~line 6499)
2. Modify `HasNakedPosition` inner loop: replace 3-branch state+type check with
   `IsBracketStopOrder(o)` call + simplified target check

**Method signatures**:
- `private static bool IsBracketStopOrder(Order o)` -- NEW method
  Parameters: `Order o` (NT8 Order object)
  Returns: `bool` -- true if order is any recognized bracket stop type
  CYC: 3
- `private static bool HasNakedPosition(Account acct)` -- existing, modified
  Signature unchanged; inner loop logic replaced
  CYC: 8 -> 7

**Tests** (xUnit [Fact], file: `src/PropTraderTools/Tests/B194Tests.cs`):
- `HasNakedPosition_ReturnsFalse_WhenOnlyTriggerPendingStopExists`
  Assert: account with position + one TriggerPending Market stop (StopPrice>0) returns false.
  Mechanism: stub Account with Position(qty>0) + Order(TriggerPending,Market,StopPrice=4100).
- `IsBracketStopOrder_ReturnsTrue_ForTriggerPendingMarketWithStopPrice`
  Assert: IsBracketStopOrder returns true for Order(TriggerPending, Market, StopPrice=4100).
  Mechanism: direct static call to CopyEngine.IsBracketStopOrder (internal visibility needed).
- `IsBracketStopOrder_ReturnsTrue_ForWorkingStopMarket`
  Assert: IsBracketStopOrder returns true for Order(Working, StopMarket, StopPrice=0).
  Mechanism: direct static call.
- `IsBracketStopOrder_ReturnsFalse_ForWorkingLimitOrder`
  Assert: IsBracketStopOrder returns false for Order(Working, Limit, StopPrice=0).
  Mechanism: direct static call -- confirms Limit orders are not misclassified as stops.

Note on test visibility: `IsBracketStopOrder` must be `internal static` (not `private static`)
to be callable from the test project. The plan specifies `private static` for production intent;
engineer MUST use `internal static` to enable direct unit testing without NT8 runtime.

**7-Scan Checklist (T2 engineer contract)**:
- SCAN-01 (JS-021 no lock): IsBracketStopOrder is a pure predicate; HasNakedPosition modified body has no lock ✓
- SCAN-02 (JS-033 no async void): no async in either method ✓
- SCAN-03 (JS-066 CYC<=8): HasNakedPosition CYC=7, IsBracketStopOrder CYC=3 ✓
- SCAN-04 (JS-001 no throw): no throw in helper or inner loop ✓
- SCAN-05 (ASCII-only): "IsBracketStopOrder" identifier is ASCII; no new string literals ✓
- SCAN-06 (DateTime.UtcNow only): no DateTime changes ✓
- SCAN-07 (no FontFamily/hex colors): no UI changes ✓

---

### T3 -- FlattenOneAccount Belt-and-Suspenders Debounce (F4)

**Spec requirements**: BUG-3 (P0) residual path
**DW cards closed**: DW-B194-04
**File**: `src/PropTraderTools/CopyEngine.cs`

**Changes**:
1. Add `_flattenFilledTicks` ConcurrentDictionary field (~line 376)
2. Insert debounce check in `FlattenOneAccount` after in-flight scan, before FindPosition
3. Insert timestamp record in `FlattenOneAccount` before acc.Submit(flatten)

**Field declaration**:
```
private readonly ConcurrentDictionary<string, long> _flattenFilledTicks
    = new ConcurrentDictionary<string, long>(StringComparer.Ordinal);
```

**Method signature** (unchanged):
- `private void FlattenOneAccount(Account acc, Instrument instrument)` -- existing, modified
  CYC: 6 -> 7

**Tests** (xUnit [Fact], file: `src/PropTraderTools/Tests/B194Tests.cs`):
- `FlattenOneAccount_SkipsWhenFlattenWithin500ms`
  Assert: second call to FlattenOneAccount for same account within 500ms emits "flat-debounce skip"
  via StatusUpdate and does NOT call acc.Submit a second time.
  Mechanism: call FlattenOneAccount twice in rapid succession with stubbed Account+Instrument;
  capture StatusUpdate event; assert "flat-debounce skip" appears in second call's output;
  assert acc.Submit call count = 1.

**7-Scan Checklist (T3 engineer contract)**:
- SCAN-01 (JS-021 no lock): _flattenFilledTicks uses ConcurrentDictionary.GetOrAdd/AddOrUpdate only ✓
- SCAN-02 (JS-033 no async void): FlattenOneAccount is synchronous void ✓
- SCAN-03 (JS-066 CYC<=8): FlattenOneAccount CYC=7 ✓
- SCAN-04 (JS-001 no throw): debounce check and timestamp record use no throw ✓
- SCAN-05 (ASCII-only): "flat-debounce skip", "_flattenFilledTicks" are ASCII ✓
- SCAN-06 (DateTime.UtcNow only): debounce uses Environment.TickCount (not DateTime) ✓
- SCAN-07 (no FontFamily/hex colors): no UI changes ✓

---

## 11. Deferred Work Register

### DW Cards Closed by B194

| DW ID | Description | Closed By | Status |
|-------|-------------|-----------|--------|
| DW-B194-01 | TryNakedDetect PTT-Copy fill false positive | T1 (F1) | CLOSED |
| DW-B194-02 | HasNakedPosition TriggerPending blind spot | T2 (F2) | CLOSED |
| DW-B194-03 | DrainThenDispatch Submitted exclusion race | T1 (F3) | CLOSED |
| DW-B194-04 | FlattenOneAccount loop belt-and-suspenders | T3 (F4) | CLOSED (implemented) |

### Open DW Cards (Carry-Forward from B193 -- NOT CLOSED by B194)

---

#### DW-B183-03: IsLaneEEligible CYC=8 Frozen

```
Priority: P1
Description: IsLaneEEligible in CopyEngine.cs is at CYC=8 (JS-066 ceiling). Any branch
  addition to IsLaneEEligible without prior helper extraction is an automatic P1 JS-066
  violation. Not modified in B183-B194.
  Future engineers modifying Lane E eligibility MUST extract a helper method first.
Blocking: No (current CYC is at limit, not over it; blocks only future branch additions)
Target Block: When Lane E is next modified
Status: OPEN (CARRIED FORWARD from B183 via B184-B193, now B194)
Owner: Director
Source: docs/brain/B191/06-deferred-backlog.md -> docs/brain/B193/06-deferred-backlog.md
```

---

#### DW-B192-SINST-01: SetInstrument() CYC at Ceiling 8

```
Priority: P2
Description: SetInstrument() in TradeCopierPanel.cs is at CYC=8 (JS-066 ceiling). Any branch
  addition to SetInstrument() without prior helper extraction is an automatic P1 JS-066
  violation. Not modified in B192-B194.
Blocking: No (current CYC is at limit, not over it; blocks only future branch additions)
Target Block: When SetInstrument() is next modified
Status: OPEN (opened B193, carried forward to B194)
Owner: Director
Source: docs/brain/B193/06-deferred-backlog.md
```

---

#### DW-B193-01: OnQuickAllBufferChanged Old Label Strings

```
Priority: P3
Description: OnQuickAllBufferChanged in TradeCopierPanel.cs (~line 1027-1029) still uses
  old label strings "Quick ALL" and "QAll2t" in the singleton-sink callback path.
  Not modified in B193-B194. Low severity: visual label shows old name only on singleton path.
Blocking: No
Target Block: B195+ when OnQuickAllBufferChanged is next touched
Status: OPEN (opened B193, carried forward to B194)
Owner: Director
Source: docs/brain/B193/06-deferred-backlog.md
```

---

## 12. Jane Street DNA Compliance Summary

| Rule | Requirement | B194 Status |
|------|-------------|-------------|
| JS-021 | No lock() | All 4 fixes use only ConcurrentDictionary ops -- zero lock() ✓ |
| JS-033 | No async void | No async void in any new or modified method ✓ |
| JS-066 | CYC <= 8 | All methods at CYC <= 7 post-fix; new helper CYC=3 ✓ |
| JS-001 | No throw in hot paths | No throw in any fix ✓ |
| ASCII-only | All string literals and identifiers ASCII | All new identifiers and strings ASCII ✓ |
| DateTime.UtcNow | No DateTime.Now | Debounce uses Environment.TickCount, not DateTime ✓ |
| No FontFamily | No UI font changes | No UI code in any fix ✓ |

---

## 13. B194 Test File

**New test file**: `src/PropTraderTools/Tests/B194Tests.cs`

All tests use xUnit `[Fact]` attribute. No NUnit. No MSTest. No NT8 runtime required.

Tests requiring `IsBracketStopOrder` visibility:
- Engineer MUST declare `IsBracketStopOrder` as `internal static` (not `private static`).
- The test project uses `InternalsVisibleTo` (already configured in CopyEngine.cs or .csproj).
- If not yet configured, engineer adds `[assembly: InternalsVisibleTo("PropTraderTools.Tests")]`
  to CopyEngine.cs namespace level.

**Full test list**:

| # | Test Name | Ticket | Asserts |
|---|-----------|--------|---------|
| 1 | `TryNakedDetect_SkipsPTTCopyFill_NoNakedDetectorCalled` | T1 | No flatten queued on PTT-Copy Filled |
| 2 | `DrainThenDispatch_IncludesSubmittedInEntryCandidates` | T1 | Cancel sent for Submitted PTT-Copy on drain-3 |
| 3 | `HasNakedPosition_ReturnsFalse_WhenOnlyTriggerPendingStopExists` | T2 | Returns false with TriggerPending stop |
| 4 | `IsBracketStopOrder_ReturnsTrue_ForTriggerPendingMarketWithStopPrice` | T2 | TriggerPending+Market+StopPrice>0 recognized |
| 5 | `IsBracketStopOrder_ReturnsTrue_ForWorkingStopMarket` | T2 | Working StopMarket recognized |
| 6 | `IsBracketStopOrder_ReturnsFalse_ForWorkingLimitOrder` | T2 | Limit orders not misclassified as stops |
| 7 | `FlattenOneAccount_SkipsWhenFlattenWithin500ms` | T3 | Debounce prevents second flatten within 500ms |

---

*Written by: ptt-architect*
*Epic: B194*
*Phase: 1 (Architecture Planning)*
*Required for Phase 2 review gate (ptt-plan-reviewer).*
*Supersedes: n/a (first plan for B194)*
