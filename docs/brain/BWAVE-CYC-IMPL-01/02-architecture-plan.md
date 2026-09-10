# BWAVE-CYC-IMPL-01 Architecture Plan
**Epic:** BWAVE-CYC-IMPL-01
**Phase:** 1 — Architecture
**Author:** ptt-architect
**Date:** 2026-08
**Status:** PLAN_COMPLETE
**Closes DW item:** DW-09-01 (OPEN, from PTT-REPAIRS-09-OBFUSC-ATTR/06-deferred-backlog.md)

---

## LANE-SPLIT GATE RESULT: SINGLE-PIPELINE

| Question | Answer | Rationale |
|----------|--------|-----------|
| Q1. Same method or within 50 lines? | All 70 methods are in a single new insertion block in one file | Single-file, single insertion anchor — no split warranted |
| Q2. Fix B design depends on Fix A final design? | N/A — one mission (70 stubs, one file, no cross-dependency between tickets) | Each ticket independently compiles and tests |
| Q3. Each fix has standalone value if the other is blocked? | Yes — each ticket (T1–T5) can be merged and tested independently | All 5 tickets target the same insertion region; T1 does not block T2 |
| Q4. Each fix has an independent SIM verification path? | Yes — each ticket verifies with `dotnet test --filter "FullyQualifiedName~<TestClass>"` | Test class per ticket maps 1-to-1 |

**Result: SINGLE-PIPELINE.** 70 methods, 1 target file, 5 tickets. No lane split warranted.

---

## Mission Summary

DW-09-01 requires implementing 70 missing private helper methods in `src/PropTraderTools/CopyEngine.cs`.
These methods are referenced by reflection in 5 xUnit test classes. All tests are currently tagged
`[Fact(Skip = "obfuscation: ...")]` because AgileDotNetRT renames private members and the methods
do not yet exist. This epic provides the stub implementations so:

1. The methods exist and are findable by reflection.
2. Each method carries `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
   to prevent AgileDotNetRT from renaming them.
3. After DW-09-04 (skip-removal epic, out of scope here), tests can be un-skipped.

**What this epic does NOT do:**
- Does not remove any `[Fact(Skip)]` annotations (DW-09-04, out of scope).
- Does not fix binding-flag mismatches for LogBeSlotEviction or GetSenderAccountName (DW-09-02/03, out of scope).
- Does not implement full production logic in any stub (engineer follow-up epic).
- Does not modify any existing CopyEngine.cs method.
- Does not create any new .cs files.

---

## Source File Analysis

| Property | Value |
|----------|-------|
| File | `src/PropTraderTools/CopyEngine.cs` |
| Total lines (current) | 7923 |
| Target class | `CopyEngine` (L91–L7922) |
| Insertion anchor | Before `PendingDispatchDrain` inner class (before current L7876) |
| Methods already present (not in 70) | `LogBeSlotEviction` (L1779, private static), `GetSenderAccountName` (L6970, internal static), `LogDiagOrderCount` (L6424, private instance) |

### Existing patterns to follow

```csharp
// Header comment: what the method does, CCN, JS rules.
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsXxxYyy(Order order)
{
    return false;  // stub
}
```

All 8-space indentation (matches surrounding code). No blank lines between attribute and method signature.

---

## Test Class Analysis

### B79CancelRaceGuardTests (~L5829–L6461)

**GetMethod helper:** `typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)`

All 24 methods it tests must be **private instance** on `CopyEngine`.

Special notes:
- `IsPositionFlatOrMissing` uses explicit inline `BindingFlags.NonPublic | BindingFlags.Static` → must be **private static**
- `IsReArmedAtmBracketCleanupRequired` uses explicit inline `BindingFlags.NonPublic | BindingFlags.Instance` → private instance (matches class helper)

Tests assert only `Assert.NotNull(m)` — no parameter count or invocation assertions in this class.

| Method | Test names |
|--------|-----------|
| TryFireImmediateBeIfAlreadyAtLevel | _ShouldReturnFalse_WhenTickSizeIsZero, _WhenPriceIsZero, _ShouldReturnTrue_WhenLong..., _WhenShort... |
| IsPendingBeTriggerMet | _ShouldReturnFalse_WhenRefPriceIsZero, _WhenLong..., _ShouldReturnTrue_WhenLong..., _WhenShort... |
| IsEligibleBeTargetOrder | _WhenOrderStateIsNotInSnapshot, _WhenInstrumentDoesNotMatch, _WhenOrderTypeIsNotLimit |
| IsNativeAtmTargetOrder | _ShouldReturnTrue_WhenNameIsTarget1, _ShouldReturnFalse_WhenNameIsTarget0 |
| IsPttBeOrQxTargetOrder | _WhenNameStartsWithPttQxT1, _WhenNameStartsWithPttBeTarget |
| RegisterBeRetryIfNoTargets | _ShouldNotRegister_WhenIsRetryIsTrue, _WhenPositionIsFlat, _ShouldRegister... |
| RegisterPartialTargetBeRetry | _ShouldNotRegister_..., _ShouldRegisterSlot_... |
| CancelExistingStpDragOrders | _ShouldCancelMatchingLiveStpDragOrder |
| CancelExistingTgtDragOrders | _ShouldCancelMatchingLiveTgtDragOrder |
| SubmitReplacementStopLeg | _WhenCreateOrderReturnsNull, _ShouldUseLeaderQuantity... |
| SubmitReplacementTargetLeg | _WhenCreateOrderReturnsNull, _ShouldUseLeaderQuantity... |
| IsReArmedAtmBracketCleanupRequired | _WhenNotWorkingOrAccepted, _WhenNameDoesNotStartWithPttQxT, _WhenTtlHasExpired, _ShouldReturnTrue_WhenAllConditionsMet |
| FindMatchingNativeAtmBracket | _ShouldReturnNull_..., _ShouldReturnOrder_... |
| TryFindRuleAndFollowerIndex | _WhenInstrumentDoesNotMatch, _WhenFollowerAccountMatches, _ShouldSetFollowerIndex... |
| HasActiveQxOrdersForInstrument | _WhenPttQxOrderIsWorking, _WhenNoQxOrdersExist, _WhenQxOrderIsFilledNotWorking |
| SyncAtmFollowerStopBracket | _WhenStopPriceIsZero, _ShouldCallResubmitTarget... |
| CancelStaleTgtDragOrders | _ShouldCancelMatchingWorkingOrder, _ShouldSkipNonMatchingOrders |
| CreateAndSubmitReplacementTarget | _ShouldReturnNull_..., _ShouldUseLeaderQuantity... |
| HasInFlightFlattenOrder | _ShouldReturnTrue_..., _ShouldReturnFalse_... |
| IsPositionFlatOrMissing | _ShouldReturnTrue_WhenPositionIsNull, _WhenPositionQuantityIsZero |
| IsLeaderTargetOrder | _ShouldReturnTrue_..., _WhenNotWorking, _WhenNameDoesNotStartWithTarget, _WhenSixthCharIsNotDigit |
| ResubmitFollowerEntry | _WhenPriceChangeIsWithinTickSize, _ShouldUseStopPrice..., _ShouldPreloadDedupCache... |
| IsLeaderAccountForInstrument | _ShouldReturnTrue_..., _ShouldReturnFalse_... |
| CancelStaleCascadeTgtDrag | _ShouldCancelMatchingWorkingOrder, _ShouldSkipNonWorkingOrders |

---

### BwaveCycT1R1BeHelperTests (~L6475–L6684)

**GetMethod helper:** `typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)`

All 12 new methods must be **private instance** on `CopyEngine`.

**Critical:** `SelectBeRefPriceByDirection` is INVOKED in tests (L6511–L6543). Its signature and logic must be correct.

| Method | Test type | Special |
|--------|-----------|---------|
| GetMarketBidPrice | NotNull only | — |
| GetMarketAskPrice | NotNull only | — |
| GetBeTickSize | NotNull only | — |
| SelectBeRefPriceByDirection | **Invoked** (4 tests) | Signature: `(bool isLong, double bid, double ask) → double`; logic verified |
| FireBeAndNotifyEvent | NotNull only | — |
| ShouldFireBeImmediately | NotNull only | — |
| CompleteBeArming | NotNull only | — |
| TryClaimPendingBeSlot | NotNull only | — |
| GetSlotInstrumentName | NotNull only | — |
| GetSlotAccountName | NotNull only | — |
| RaisePendingBeFiredEvent | NotNull only | — |
| SettleAndFirePendingBe | NotNull only | — |

**SelectBeRefPriceByDirection logic (from test assertions):**
- `(true,  100.25, 100.50)` → 100.25 (long + bid positive → bid)
- `(true,  0.0,   100.50)` → 100.50 (long + bid zero → ask fallback)
- `(false, 100.25, 100.50)` → 100.50 (short + ask positive → ask)
- `(false, 100.25, 0.0)`   → 100.25 (short + ask zero → bid fallback)

Implementation: `return isLong ? (bid > 0 ? bid : ask) : (ask > 0 ? ask : bid);`
CYC = 4 (two nested ternaries). JS-013 ✓

---

### BwaveCycTaR2HelperTests (~L6692–L6811)

**GetMethod helper:** `typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)`

All 5 new methods must be **private instance** on `CopyEngine`.

| Method | Tests |
|--------|-------|
| HasValidTargetNameSuffix | NotNull only |
| SelectBeTargetList | NotNull only |
| IsBeTargetActiveState | NotNull only |
| IsBeTargetPendingChangeState | NotNull only |
| IsBeTargetSnapshotState | NotNull only |

Note: `IsLeaderTargetOrder` and `IsEligibleBeTargetOrder` also tested here (already in Group A; same binding flags confirm private instance).

---

### BwaveCycTaR3HelperTests (~L6818–L7111)

**GetMethod helper:** `typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)`

All 24 new methods must be **private instance** on `CopyEngine`.

Special notes:
- `TrySyncAtmBrackets` uses explicit inline `BindingFlags.NonPublic | BindingFlags.Instance` (confirms private instance).
- `LogBeSlotEviction` is tested here but uses GetMethod (NonPublic|Instance). The existing implementation at L1779 is `private static`. Tests will still fail to find it by Instance binding flags. This is the DW-09-02 mismatch. **Do NOT add a second LogBeSlotEviction.** It is NOT in the 70-method task list.

| Method | Tests |
|--------|-------|
| TrySyncAtmBrackets | NotNull only |
| TrySkipTrailingStop | NotNull only |
| SyncStandardBracket | NotNull only |
| IsPttTgtDragOrder | NotNull only |
| IsAtmTgtOrder | NotNull only |
| IsBePendingTargetOrder | NotNull only (3 tests: _WhenNameIsPttQxT1, _WhenTarget1, _WhenUnrelated) |
| IsPttBeStopRejected | NotNull only (3 tests) |
| IsPttDragOrderCancellable | NotNull only (4 tests) |
| IsPttQxTargetOrder | NotNull only |
| IsNativeAtmBeRetryTarget | NotNull only |
| IsBeRetryEligibleOrderState | NotNull only |
| IsBeRetryOrderInvalid | NotNull only |
| IsBeSlotNonTerminal | NotNull only |
| IsBeFilledWithOpenPosition | NotNull only |
| IsPttDragOrderName | NotNull only |
| IsDragInstrumentMatch | NotNull only |
| IsQxTOrderStateValid | NotNull only |
| IsQxTBracketNameValid | NotNull only |
| TryGetCleanupEntryForFollower | NotNull only (explicit inline BindingFlags) |
| IsCleanupEntryCurrentAndMatching | NotNull only (explicit inline BindingFlags) |
| SendAtmCancelReplace | NotNull only |
| TryMatchFollowerInRule | NotNull only |
| IsBeReplaceTargetValid | NotNull only |
| TryIncrementBeReplaceAttempt | NotNull only |

---

### BwaveCycTaR6HelperTests (~L7116–L7274)

**Two GetMethod helpers:**
- `GetStaticMethod`: `typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)` → **private static**
- `GetInstanceMethod`: `typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)` → **private instance**

| Method | Access | Parameter count assertion | Notes |
|--------|--------|--------------------------|-------|
| IsBracketOrderLiveState | private **static** | 1 param (L7139) | `(Order order) → bool` |
| MatchesPttReplacementName | private **static** | 3 params (L7185) | `(string a, string b, string c) → bool` |
| LogHbcDiag | private **instance** | 5 params (L7202) | `void` method, 5 params |
| ExecuteStopDragOrder | private **instance** | 5 params (L7219) | `void` method, 5 params |
| IsOrderEventProcessable | private **static** | 1 param (L7272) | `(OrderEventArgs e) → bool` |

---

## Method Inventory

Complete table of all 70 methods, sorted by group:

| # | Method Name | Return Type | Access | Static? | Source Test Class | CYC |
|---|-------------|-------------|--------|---------|-------------------|-----|
| 1 | TryFireImmediateBeIfAlreadyAtLevel | bool | private | instance | B79Cancel, T1R1Be | 1 |
| 2 | IsPendingBeTriggerMet | bool | private | instance | B79Cancel, T1R1Be | 1 |
| 3 | IsEligibleBeTargetOrder | bool | private | instance | B79Cancel, TaR2 | 1 |
| 4 | IsNativeAtmTargetOrder | bool | private | instance | B79Cancel | 1 |
| 5 | IsPttBeOrQxTargetOrder | bool | private | instance | B79Cancel | 1 |
| 6 | RegisterBeRetryIfNoTargets | void | private | instance | B79Cancel | 1 |
| 7 | RegisterPartialTargetBeRetry | void | private | instance | B79Cancel | 1 |
| 8 | CancelExistingStpDragOrders | void | private | instance | B79Cancel | 1 |
| 9 | CancelExistingTgtDragOrders | void | private | instance | B79Cancel | 1 |
| 10 | SubmitReplacementStopLeg | void | private | instance | B79Cancel | 1 |
| 11 | SubmitReplacementTargetLeg | void | private | instance | B79Cancel | 1 |
| 12 | IsReArmedAtmBracketCleanupRequired | bool | private | instance | B79Cancel | 1 |
| 13 | FindMatchingNativeAtmBracket | Order | private | instance | B79Cancel | 1 |
| 14 | TryFindRuleAndFollowerIndex | bool | private | instance | B79Cancel | 1 |
| 15 | HasActiveQxOrdersForInstrument | bool | private | instance | B79Cancel | 1 |
| 16 | SyncAtmFollowerStopBracket | void | private | instance | B79Cancel, TaR3 | 1 |
| 17 | CancelStaleTgtDragOrders | void | private | instance | B79Cancel | 1 |
| 18 | CreateAndSubmitReplacementTarget | Order | private | instance | B79Cancel | 1 |
| 19 | HasInFlightFlattenOrder | bool | private | instance | B79Cancel | 1 |
| 20 | IsPositionFlatOrMissing | bool | private | **static** | B79Cancel | 1 |
| 21 | IsLeaderTargetOrder | bool | private | instance | B79Cancel, TaR2 | 1 |
| 22 | ResubmitFollowerEntry | void | private | instance | B79Cancel | 1 |
| 23 | IsLeaderAccountForInstrument | bool | private | instance | B79Cancel | 1 |
| 24 | CancelStaleCascadeTgtDrag | void | private | instance | B79Cancel | 1 |
| 25 | GetMarketBidPrice | double | private | instance | T1R1Be | 1 |
| 26 | GetMarketAskPrice | double | private | instance | T1R1Be | 1 |
| 27 | GetBeTickSize | double | private | instance | T1R1Be | 1 |
| 28 | SelectBeRefPriceByDirection | double | private | instance | T1R1Be | **4** |
| 29 | FireBeAndNotifyEvent | void | private | instance | T1R1Be | 1 |
| 30 | ShouldFireBeImmediately | bool | private | instance | T1R1Be | 1 |
| 31 | CompleteBeArming | void | private | instance | T1R1Be | 1 |
| 32 | TryClaimPendingBeSlot | bool | private | instance | T1R1Be | 1 |
| 33 | GetSlotInstrumentName | string | private | instance | T1R1Be | 1 |
| 34 | GetSlotAccountName | string | private | instance | T1R1Be | 1 |
| 35 | RaisePendingBeFiredEvent | void | private | instance | T1R1Be | 1 |
| 36 | SettleAndFirePendingBe | void | private | instance | T1R1Be | 1 |
| 37 | HasValidTargetNameSuffix | bool | private | instance | TaR2 | 1 |
| 38 | SelectBeTargetList | System.Collections.Generic.IList<Order> | private | instance | TaR2 | 1 |
| 39 | IsBeTargetActiveState | bool | private | instance | TaR2 | 1 |
| 40 | IsBeTargetPendingChangeState | bool | private | instance | TaR2 | 1 |
| 41 | IsBeTargetSnapshotState | bool | private | instance | TaR2 | 1 |
| 42 | TrySyncAtmBrackets | bool | private | instance | TaR3 | 1 |
| 43 | TrySkipTrailingStop | bool | private | instance | TaR3 | 1 |
| 44 | SyncStandardBracket | void | private | instance | TaR3 | 1 |
| 45 | IsPttTgtDragOrder | bool | private | instance | TaR3 | 1 |
| 46 | IsAtmTgtOrder | bool | private | instance | TaR3 | 1 |
| 47 | IsBePendingTargetOrder | bool | private | instance | TaR3 | 1 |
| 48 | IsPttBeStopRejected | bool | private | instance | TaR3 | 1 |
| 49 | IsPttDragOrderCancellable | bool | private | instance | TaR3 | 1 |
| 50 | IsPttQxTargetOrder | bool | private | instance | TaR3 | 1 |
| 51 | IsNativeAtmBeRetryTarget | bool | private | instance | TaR3 | 1 |
| 52 | IsBeRetryEligibleOrderState | bool | private | instance | TaR3 | 1 |
| 53 | IsBeRetryOrderInvalid | bool | private | instance | TaR3 | 1 |
| 54 | IsBeSlotNonTerminal | bool | private | instance | TaR3 | 1 |
| 55 | IsBeFilledWithOpenPosition | bool | private | instance | TaR3 | 1 |
| 56 | IsPttDragOrderName | bool | private | instance | TaR3 | 1 |
| 57 | IsDragInstrumentMatch | bool | private | instance | TaR3 | 1 |
| 58 | IsQxTOrderStateValid | bool | private | instance | TaR3 | 1 |
| 59 | IsQxTBracketNameValid | bool | private | instance | TaR3 | 1 |
| 60 | TryGetCleanupEntryForFollower | bool | private | instance | TaR3 | 1 |
| 61 | IsCleanupEntryCurrentAndMatching | bool | private | instance | TaR3 | 1 |
| 62 | SendAtmCancelReplace | void | private | instance | TaR3 | 1 |
| 63 | TryMatchFollowerInRule | bool | private | instance | TaR3 | 1 |
| 64 | IsBeReplaceTargetValid | bool | private | instance | TaR3 | 1 |
| 65 | TryIncrementBeReplaceAttempt | bool | private | instance | TaR3 | 1 |
| 66 | IsBracketOrderLiveState | bool | private | **static** | TaR6 | 1 |
| 67 | MatchesPttReplacementName | bool | private | **static** | TaR6 | 1 |
| 68 | LogHbcDiag | void | private | instance | TaR6 | 1 |
| 69 | ExecuteStopDragOrder | void | private | instance | TaR6 | 1 |
| 70 | IsOrderEventProcessable | bool | private | **static** | TaR6 | 1 |

**Static methods (4):** IsPositionFlatOrMissing (#20), IsBracketOrderLiveState (#66), MatchesPttReplacementName (#67), IsOrderEventProcessable (#70).

---

## Implementation Design

### Group A — B79CancelRaceGuard (T1, methods 1–24)

All stubs follow the pattern below. No live logic needed — reflection target only.
`FindMatchingNativeAtmBracket` and `CreateAndSubmitReplacementTarget` return `Order` (nullable, return `null`).

```csharp
// BWAVE-CYC-IMPL-01 Group A: B79CancelRaceGuard helpers.
// All private instance. ObfuscationAttribute prevents AgileDotNetRT rename.
// Stubs only -- production logic implemented in follow-on engineering epic.
// JS-001: no throw. JS-021: no lock. JS-013: CYC=1 each. ASCII-only.

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool TryFireImmediateBeIfAlreadyAtLevel(Account acc, Instrument instr, Order tgtOrder, bool isLong, double refPx, double tickSize)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsPendingBeTriggerMet(Account acc, Instrument instr, bool isLong)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsEligibleBeTargetOrder(Order order, Instrument instr)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsNativeAtmTargetOrder(Order order)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsPttBeOrQxTargetOrder(Order order)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void RegisterBeRetryIfNoTargets(Account acc, Instrument instr, bool isRetry, int leaderCount)
{ }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void RegisterPartialTargetBeRetry(Account acc, Instrument instr, int targetsCount, int leaderCount)
{ }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void CancelExistingStpDragOrders(Account acc, Instrument instr)
{ }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void CancelExistingTgtDragOrders(Account acc, Instrument instr)
{ }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void SubmitReplacementStopLeg(Account acc, Instrument instr, Order leaderOrder, double stopPrice)
{ }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void SubmitReplacementTargetLeg(Account acc, Instrument instr, Order leaderOrder, double targetPrice)
{ }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsReArmedAtmBracketCleanupRequired(Order order, DateTime cutoff)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private Order FindMatchingNativeAtmBracket(Account acc, Instrument instr, string namePrefix)
{ return null; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool TryFindRuleAndFollowerIndex(Account acc, Instrument instr, out int followerIndex)
{ followerIndex = -1; return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool HasActiveQxOrdersForInstrument(Account acc, Instrument instr)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void SyncAtmFollowerStopBracket(Account acc, Instrument instr, Order leaderStop, double capturedPrice)
{ }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void CancelStaleTgtDragOrders(Account acc, Instrument instr, string leaderName)
{ }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private Order CreateAndSubmitReplacementTarget(Account acc, Instrument instr, Order leaderOrder, double price)
{ return null; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool HasInFlightFlattenOrder(Account acc, Instrument instr)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private static bool IsPositionFlatOrMissing(NinjaTrader.Cbi.Position pos)
{ return true; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsLeaderTargetOrder(Order order)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void ResubmitFollowerEntry(Account acc, Instrument instr, Order leaderEntry, CopyRule rule)
{ }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsLeaderAccountForInstrument(Account acc, Instrument instr)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void CancelStaleCascadeTgtDrag(Account acc, Instrument instr, string leaderName)
{ }
```

**Note on TryFindRuleAndFollowerIndex**: Uses an `out` parameter for the index. The test only verifies `Assert.NotNull(m)` so this is acceptable. If the engineer needs to change the signature for consistency, they may update the parameter list — but the method NAME is the critical thing for reflection.

---

### Group B — T1R1 BE Trigger/Arming (T2, methods 25–36)

The only method with a working implementation is `SelectBeRefPriceByDirection`.

```csharp
// BWAVE-CYC-IMPL-01 Group B: T1R1 BE immediate-fire and pending-trigger helpers.
// All private instance. ObfuscationAttribute prevents AgileDotNetRT rename.
// SelectBeRefPriceByDirection has working logic (test invokes it).
// All others are stubs. JS-001: no throw. JS-021: no lock. ASCII-only.

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private double GetMarketBidPrice(Instrument instr)
{ return 0.0; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private double GetMarketAskPrice(Instrument instr)
{ return 0.0; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private double GetBeTickSize(Instrument instr)
{ return 0.0; }

// SelectBeRefPriceByDirection: working implementation required (test invokes and asserts result).
// Long + bid>0 -> bid. Long + bid==0 -> ask. Short + ask>0 -> ask. Short + ask==0 -> bid.
// CYC=4 (two nested ternaries). JS-013 compliant.
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private double SelectBeRefPriceByDirection(bool isLong, double bid, double ask)
{
    return isLong ? (bid > 0 ? bid : ask) : (ask > 0 ? ask : bid);
}

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void FireBeAndNotifyEvent(Account acc, Instrument instr, double bePrice, bool isLong)
{ }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool ShouldFireBeImmediately(Account acc, Instrument instr, double beTarget, bool isLong)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void CompleteBeArming(Account acc, Instrument instr, int bufferTicks)
{ }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool TryClaimPendingBeSlot(string accName, Instrument instr)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private string GetSlotInstrumentName(string accName)
{ return string.Empty; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private string GetSlotAccountName(string instrName)
{ return string.Empty; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void RaisePendingBeFiredEvent(string instrName, string accName)
{ }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void SettleAndFirePendingBe(string accName, Instrument instr)
{ }
```

---

### Group C — TaR2 Target-Selection Helpers (T3, methods 37–41)

```csharp
// BWAVE-CYC-IMPL-01 Group C: TaR2 target-selection helpers.
// All private instance stubs. ObfuscationAttribute prevents AgileDotNetRT rename.
// JS-001: no throw. JS-021: no lock. JS-013: CYC=1. ASCII-only.

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool HasValidTargetNameSuffix(string orderName)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private System.Collections.Generic.IList<Order> SelectBeTargetList(Account acc, Instrument instr)
{ return new System.Collections.Generic.List<Order>(); }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsBeTargetActiveState(Order order)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsBeTargetPendingChangeState(Order order)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsBeTargetSnapshotState(Order order)
{ return false; }
```

---

### Group D — TaR3 Sync/Drag/Bracket Helpers (T4, methods 42–65)

```csharp
// BWAVE-CYC-IMPL-01 Group D: TaR3 sync/drag/bracket and BE-retry helpers.
// All private instance stubs. ObfuscationAttribute prevents AgileDotNetRT rename.
// JS-001: no throw. JS-021: no lock. JS-013: CYC=1. ASCII-only.

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool TrySyncAtmBrackets(Order leaderOrder, Account followerAcc, CopyRule rule)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool TrySkipTrailingStop(Order leaderOrder, Account followerAcc, CopyRule rule)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void SyncStandardBracket(Order leaderOrder, Account followerAcc, Instrument instr, CopyRule rule)
{ }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsPttTgtDragOrder(Order order)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsAtmTgtOrder(Order order)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsBePendingTargetOrder(Order order)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsPttBeStopRejected(Order order)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsPttDragOrderCancellable(Order order, Instrument instr)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsPttQxTargetOrder(Order order)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsNativeAtmBeRetryTarget(Order order)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsBeRetryEligibleOrderState(Order order)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsBeRetryOrderInvalid(Order order)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsBeSlotNonTerminal(string accName)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsBeFilledWithOpenPosition(Account acc, Instrument instr)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsPttDragOrderName(string orderName)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsDragInstrumentMatch(Order order, Instrument instr)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsQxTOrderStateValid(Order order)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsQxTBracketNameValid(Order order)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool TryGetCleanupEntryForFollower(string followerAccName, out object entry)
{ entry = null; return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsCleanupEntryCurrentAndMatching(object entry, Order order)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void SendAtmCancelReplace(Account acc, Order order, double newPrice)
{ }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool TryMatchFollowerInRule(Account acc, Instrument instr, out int followerIndex)
{ followerIndex = -1; return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsBeReplaceTargetValid(Order order)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool TryIncrementBeReplaceAttempt(string accName)
{ return false; }
```

**Note on out-parameter methods (TryGetCleanupEntryForFollower, TryMatchFollowerInRule):**
Tests only assert `Assert.NotNull(m)`. The `out` parameter type for `entry` is `object` as a placeholder. The engineer implementing the full logic will replace `object` with the correct internal type (likely a cleanup-entry struct or the CopyRule-follower index). The method NAME is what the test resolves by reflection.

---

### Group E — TaR6 Static/Instance Predicate Helpers (T5, methods 66–70)

```csharp
// BWAVE-CYC-IMPL-01 Group E: TaR6 static predicate and instance helpers.
// IsBracketOrderLiveState, MatchesPttReplacementName, IsOrderEventProcessable: private static.
// LogHbcDiag, ExecuteStopDragOrder: private instance.
// ObfuscationAttribute prevents AgileDotNetRT rename.
// JS-001: no throw. JS-021: no lock. JS-013: CYC=1. ASCII-only.

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private static bool IsBracketOrderLiveState(Order order)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private static bool MatchesPttReplacementName(string leaderName, string suffix, string followerName)
{ return false; }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void LogHbcDiag(Order leaderOrder, Order followerOrder, CopyRule rule, double price, string tag)
{ }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void ExecuteStopDragOrder(Account acc, Instrument instr, Order leaderOrder, double stopPrice, CopyRule rule)
{ }

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private static bool IsOrderEventProcessable(NinjaTrader.Cbi.OrderEventArgs e)
{ return false; }
```

---

## Insertion Plan

**Insertion point:** Before the `PendingDispatchDrain` inner class declaration. Current line L7876.

All 70 methods are inserted as a single contiguous block, ordered Group A → B → C → D → E.

**Header for the block:**
```csharp
        // ===========================================================================
        // BWAVE-CYC-IMPL-01: 70 extracted helper method stubs (Groups A-E)
        // Each method carries ObfuscationAttribute to prevent AgileDotNetRT rename.
        // Stubs provide reflection targets for obfuscation-skipped xUnit tests.
        // Production logic is implemented in the follow-on engineering epic.
        // JS-001: no throw. JS-021: no lock. JS-013: CYC<=4. ASCII-only. .NET 4.8.
        // ===========================================================================
```

**Indentation:** 8 spaces (2 levels: namespace + class). Matches surrounding code.

**No existing method modified.** No existing line removed. Pure insertion.

---

## Ticket Grouping

| Ticket | Group | Methods | Test Class | CYC budget |
|--------|-------|---------|------------|------------|
| T1 | A | 24 methods (#1–24) | B79CancelRaceGuardTests | All CYC=1; total <= 24 |
| T2 | B | 12 methods (#25–36) | BwaveCycT1R1BeHelperTests | CYC=1 each except #28 (CYC=4); total <= 15 |
| T3 | C | 5 methods (#37–41) | BwaveCycTaR2HelperTests | All CYC=1; total <= 5 |
| T4 | D | 24 methods (#42–65) | BwaveCycTaR3HelperTests | All CYC=1; total <= 24 |
| T5 | E | 5 methods (#66–70) | BwaveCycTaR6HelperTests | All CYC=1; total <= 5 |

**Grand total:** 70 methods. All in `src/PropTraderTools/CopyEngine.cs`. Each ticket is independently mergeable.

**Ticket verification command per ticket:**
- T1: `dotnet test --no-build --filter "FullyQualifiedName~B79CancelRaceGuardTests"`
- T2: `dotnet test --no-build --filter "FullyQualifiedName~BwaveCycT1R1BeHelperTests"`
- T3: `dotnet test --no-build --filter "FullyQualifiedName~BwaveCycTaR2HelperTests"`
- T4: `dotnet test --no-build --filter "FullyQualifiedName~BwaveCycTaR3HelperTests"`
- T5: `dotnet test --no-build --filter "FullyQualifiedName~BwaveCycTaR6HelperTests"`

Note: Tests remain `[Fact(Skip = "obfuscation: ...")]` after insertion — they will not un-skip until DW-09-04. The verification command confirms the methods exist by running tests that have no Skip (if any) and by confirming build passes.

Build verification: `powershell -File .\scripts\build_readiness.ps1`

### 7-Scan Checklist — T1 (B79CancelRaceGuardTests, methods #1–#24)

| Scan | Check | Result |
|------|-------|--------|
| SCAN-01 | No `lock()` added | PASS — Group A stubs contain no lock() calls |
| SCAN-02 | No `DateTime.Now` added | PASS — no date/time access in any Group A stub |
| SCAN-03 | ASCII-only strings in additions | PASS — all identifiers and literals are ASCII |
| SCAN-04 | No `FontFamily` added | PASS — N/A |
| SCAN-05 | No hardcoded hex colors | PASS — N/A |
| SCAN-06 | No new `throw` statements | PASS — stubs return false/null/default only |
| SCAN-07 | Hard-link sync after src edit | REQUIRED — run `powershell -File .\deploy-sync.ps1` after CopyEngine.cs is modified |

### 7-Scan Checklist — T2 (BwaveCycT1R1BeHelperTests, methods #25–#36)

| Scan | Check | Result |
|------|-------|--------|
| SCAN-01 | No `lock()` added | PASS — Group B stubs contain no lock() calls |
| SCAN-02 | No `DateTime.Now` added | PASS — no date/time access in any Group B stub |
| SCAN-03 | ASCII-only strings in additions | PASS — all identifiers and literals are ASCII |
| SCAN-04 | No `FontFamily` added | PASS — N/A |
| SCAN-05 | No hardcoded hex colors | PASS — N/A |
| SCAN-06 | No new `throw` statements | PASS — stubs return false/null/0.0/string.Empty only |
| SCAN-07 | Hard-link sync after src edit | REQUIRED — run `powershell -File .\deploy-sync.ps1` after CopyEngine.cs is modified |

### 7-Scan Checklist — T3 (BwaveCycTaR2HelperTests, methods #37–#41)

| Scan | Check | Result |
|------|-------|--------|
| SCAN-01 | No `lock()` added | PASS — Group C stubs contain no lock() calls |
| SCAN-02 | No `DateTime.Now` added | PASS — no date/time access in any Group C stub |
| SCAN-03 | ASCII-only strings in additions | PASS — all identifiers and literals are ASCII |
| SCAN-04 | No `FontFamily` added | PASS — N/A |
| SCAN-05 | No hardcoded hex colors | PASS — N/A |
| SCAN-06 | No new `throw` statements | PASS — stubs return false/empty list only |
| SCAN-07 | Hard-link sync after src edit | REQUIRED — run `powershell -File .\deploy-sync.ps1` after CopyEngine.cs is modified |

### 7-Scan Checklist — T4 (BwaveCycTaR3HelperTests, methods #42–#65)

| Scan | Check | Result |
|------|-------|--------|
| SCAN-01 | No `lock()` added | PASS — Group D stubs contain no lock() calls |
| SCAN-02 | No `DateTime.Now` added | PASS — no date/time access in any Group D stub |
| SCAN-03 | ASCII-only strings in additions | PASS — all identifiers and literals are ASCII |
| SCAN-04 | No `FontFamily` added | PASS — N/A |
| SCAN-05 | No hardcoded hex colors | PASS — N/A |
| SCAN-06 | No new `throw` statements | PASS — stubs return false/null/default only |
| SCAN-07 | Hard-link sync after src edit | REQUIRED — run `powershell -File .\deploy-sync.ps1` after CopyEngine.cs is modified |

### 7-Scan Checklist — T5 (BwaveCycTaR6HelperTests, methods #66–#70)

| Scan | Check | Result |
|------|-------|--------|
| SCAN-01 | No `lock()` added | PASS — Group E stubs contain no lock() calls |
| SCAN-02 | No `DateTime.Now` added | PASS — no date/time access in any Group E stub |
| SCAN-03 | ASCII-only strings in additions | PASS — all identifiers and literals are ASCII |
| SCAN-04 | No `FontFamily` added | PASS — N/A |
| SCAN-05 | No hardcoded hex colors | PASS — N/A |
| SCAN-06 | No new `throw` statements | PASS — stubs return false only |
| SCAN-07 | Hard-link sync after src edit | REQUIRED — run `powershell -File .\deploy-sync.ps1` after CopyEngine.cs is modified |

---

## V12 DNA Checklist

| Rule | Status | Notes |
|------|--------|-------|
| JS-001: No throw | ✓ PASS | All stubs return false/null/default or void. No throw anywhere. |
| JS-002: Return-type discipline | ✓ PASS | Predicates return bool. Void methods have void. Get* return typed values. |
| JS-009/021: No lock() | ✓ PASS | Zero lock() calls in any of the 70 stubs. |
| JS-013: CYC <= 8 | ✓ PASS | Max CYC=4 (SelectBeRefPriceByDirection). All others CYC=1. |
| ASCII-only | ✓ PASS | All strings in stubs are ASCII. No Unicode, no curly quotes. |
| No DateTime.Now | ✓ PASS | No date/time access in any stub. |
| No FontFamily | ✓ PASS | Not applicable. |
| No hex colors | ✓ PASS | Not applicable. |
| ObfuscationAttribute | ✓ PASS | Each of the 70 methods has `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`. |
| Static vs Instance | ✓ PASS | IsPositionFlatOrMissing (#20), IsBracketOrderLiveState (#66), MatchesPttReplacementName (#67), IsOrderEventProcessable (#70) are `private static`. All others are `private instance`. |
| .NET 4.8 compatible | ✓ PASS | No switch expressions, no record types, no init accessors, no C# 8+ features. |
| No modification to existing methods | ✓ PASS | Pure insertion only. |
| No removal of test Skip annotations | ✓ PASS | DW-09-04 is out of scope. |
| ObfuscationAttribute on pre-existing LogBeSlotEviction | ✓ PASS | Already present at L1778. Not in the 70-method list. |
| ObfuscationAttribute on pre-existing GetSenderAccountName | ✓ PASS | Already present at L6969. Not in the 70-method list. |

---

## Binding Flags Analysis

| Method | Test Binding Flags | Method Type in Plan | Will Reflection Find It? |
|--------|-------------------|---------------------|--------------------------|
| All Group A except IsPositionFlatOrMissing | NonPublic \| Instance | private instance | YES ✓ |
| IsPositionFlatOrMissing | NonPublic \| **Static** | private **static** | YES ✓ |
| All Group B | NonPublic \| Instance | private instance | YES ✓ |
| All Group C | NonPublic \| Instance | private instance | YES ✓ |
| All Group D | NonPublic \| Instance | private instance | YES ✓ |
| IsBracketOrderLiveState | NonPublic \| **Static** | private **static** | YES ✓ |
| MatchesPttReplacementName | NonPublic \| **Static** | private **static** | YES ✓ |
| LogHbcDiag | NonPublic \| Instance | private instance | YES ✓ |
| ExecuteStopDragOrder | NonPublic \| Instance | private instance | YES ✓ |
| IsOrderEventProcessable | NonPublic \| **Static** | private **static** | YES ✓ |
| LogBeSlotEviction (NOT in 70) | NonPublic \| Instance (wrong — DW-09-02) | private static (existing) | **NO** (DW-09-02 binding flag fix, out of scope) |
| GetSenderAccountName (NOT in 70) | NonPublic \| Instance (wrong — DW-09-03) | internal static (existing) | **NO** (DW-09-03 binding flag fix, out of scope) |

---

## Risk Register

| ID | Risk | Severity | Mitigation |
|----|------|----------|------------|
| R1 | `TryFindRuleAndFollowerIndex` and `TryGetCleanupEntryForFollower` use `out` parameters — some old .NET 4.8 reflection GetMethod overloads have trouble finding methods with out params by name alone | Low | Standard `GetMethod(name, BindingFlags)` finds methods by name regardless of parameter types; out params don't affect name lookup |
| R2 | `TryGetCleanupEntryForFollower` uses `out object entry` as placeholder — real implementation will need the correct type | Low | Stub compiles and is reflectable. Engineer updates type in follow-on epic without changing method name |
| R3 | `SelectBeRefPriceByDirection` is invoked by tests (not just existence-checked). Signature must be exactly `(bool isLong, double bid, double ask)` | Medium | Test at L6511 confirmed: `new object[] { true, 100.25, 100.50 }` — three params. Signature in plan is correct. |
| R4 | `LogHbcDiag` and `ExecuteStopDragOrder` have 5 params each (verified by test assertions). Incorrect parameter count = test fails | Medium | Plan specifies exactly 5 params each. Engineer must NOT add or remove params. Parameter types may be adjusted if needed but count is fixed at 5. |
| R5 | `MatchesPttReplacementName` has 3 params (verified by test). Fixed count. | Low | Plan specifies exactly 3 string params. Count is fixed. |
| R6 | `IsBracketOrderLiveState` has 1 param (verified by test). | Low | Plan specifies exactly `(Order order)`. Count fixed. |
| R7 | `IsOrderEventProcessable` has 1 param (verified by test). | Low | Plan specifies exactly `(OrderEventArgs e)`. Count fixed. |
| R8 | Line number drift — CopyEngine.cs is large (7923 lines). If concurrent edits happen, insertion point may shift | Low | Engineer inserts by searching for the `PendingDispatchDrain` class declaration text, not a fixed line number |
| R9 | `using` namespace for `OrderEventArgs` — must be fully qualified as `NinjaTrader.Cbi.OrderEventArgs` if not already in scope | Low | Existing code uses `NinjaTrader.Cbi.OrderEventArgs` in the `using NinjaTrader.Cbi` directive at top of file; type is in scope |
| R10 | `CopyRule` type reference — CopyRule is an inner class of CopyEngine; in-scope within CopyEngine. | None | Already used in dozens of existing methods. No risk. |

---

## DEFERRED WORK ITEMS (not in scope for this epic)

| ID | Description |
|----|-------------|
| DW-09-02 | Fix `LogBeSlotEviction` binding flags in test: NonPublic\|Instance → NonPublic\|Static. After DW-09-01 (this epic) confirms method remains private static. |
| DW-09-03 | Fix `GetSenderAccountName` binding flags in TaR2 test: NonPublic\|Instance → NonPublic\|Static or NonPublic\|Instance (per the T1R1 NT8-skip tests). |
| DW-09-04 | Remove all 137 obfuscation Skip annotations after each method is implemented, binding flags verified, and ObfuscationAttribute confirmed. |
| BWAVE-CYC-LOGIC | Implement actual production logic for all 70 stubs (follow-on engineering epic). |

---

## PLAN_COMPLETE
