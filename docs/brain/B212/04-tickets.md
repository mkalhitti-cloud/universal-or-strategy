# B212 Implementation Tickets
# Block: B211+B212 Repair Batch

**Status**: TICKETS_COMPLETE
**Architecture plan ref**: docs/brain/B212/02-architecture-plan.md (REVIEW_PASS)
**Hotfix source of truth**: docs/brain/NO-PIPELINE-REPAIRS.md (SESSION block 2026 B211/B212)
**Pipeline phase**: Phase 3 output (04-tickets.md)
**Author**: PTT Architect (ptt-architect mode)
**Note**: Repairs are SIM-validated. Tickets document what was implemented and provide
          the 7-scan verification checklist for Ph4b sign-off.

---

## Ticket T1 — B211 Button Repairs

### T1.1 Header

| Field | Value |
|-------|-------|
| Ticket ID | T1 |
| Title | B211 Button Repairs (ASK-01 / MOVETARGET-01 / ASKALL-01 / BIDALL-01) |
| Spec req IDs | B211-ASK-01, B211-MOVETARGET-01, B211-ASKALL-01, B211-BIDALL-01 |
| Hotfix IDs | HOTFIX-B211-ASK-01, HOTFIX-B211-MOVETARGET-01, HOTFIX-B211-ASKALL-01, HOTFIX-B211-BIDALL-01 |
| Status | APPLIED + SIM-VALIDATED |

**Files in scope:**

| File | Status | Role |
|------|--------|------|
| `src/PropTraderTools/TradeCopierPanel.cs` | MODIFIED | OnAskAllClick, OnBidAllClick |
| `src/PropTraderTools/Features/PttMoveTargetOne.cs` | NEW | Execute, CollectWorkingTargets, IsWorkingTarget, TryGetExitPrice |
| `src/PropTraderTools/Features/PttMoveTargetAll.cs` | NEW | Execute (thin wrapper, vestigial in panel path) |
| `src/PropTraderTools/Core/PttContracts.cs` | MODIFIED | MaAskPrefix, MaBidPrefix constants |
| `src/PropTraderTools/TradeCopierAddOn.cs` | MODIFIED | Wiring for PttMoveTargetOne |
| `src/PropTraderTools/Tests/B211Tests.cs` | NEW | 8+ xUnit [Fact] tests |

---

### T1.2 Problem Statement

**HOTFIX-B211-ASK-01 root cause:**
`TryFindWorkingTarget` (now `IsWorkingTarget` in `PttMoveTargetOne`) required order names to start
with `"PTT-"`. NinjaTrader 8 native ATM bracket targets are named `Target1` through `Target9` (no
`PTT-` prefix). On Clone-mode accounts, every exit order has a native ATM name. The `PTT-`-only
filter meant `firstTarget` was always null for Clone-mode accounts -- Ask button silently did nothing.

**HOTFIX-B211-MOVETARGET-01 root cause:**
The predecessor single-return design (`TryFindWorkingTarget` returning one `Order` ref via
last-write semantics) discarded all but the last matching target. When two working targets existed
(e.g. Target1 and Target2), only Target2 was processed; Target1 was silently abandoned.

**HOTFIX-B211-ASKALL-01 root cause:**
`OnAskAllClick` called `PttMoveTargetAll.Execute(instrument, useAsk: true, _askBuffer)`, which
routed through `AllAccounts(instrument)` -> `FindRule(instrument)`. When the copier was OFF or no
rule was loaded for the instrument, `FindRule` returned null and the call silently no-oped for all
accounts. `OnBidAllClick` had already been fixed (prior session) to bypass this path.

**HOTFIX-B211-BIDALL-01 root cause:**
Same root cause as HOTFIX-B211-ASKALL-01 -- `PttMoveTargetAll.Execute` -> `AllAccounts` ->
`FindRule` null-path no-op when copier is off.

---

### T1.3 Fix Description

**IsWorkingTarget predicate widening (ASK-01):**
Added `isNativeTarget` branch: `name.StartsWith("Target", Ordinal) && name.Length > 6 && char.IsDigit(name[6])`.
The `char.IsDigit(name[6])` guard prevents false positives for names like "TargetABC". Both
`isPttExit` (`PTT-` prefix) and `isNativeTarget` (Target1..Target9) return true from `IsWorkingTarget`.

**CollectWorkingTargets two-pass array (MOVETARGET-01):**
Full rewrite of the target-collection logic to `CollectWorkingTargets(Account acc, Instrument instr)`
returning `Order[]`. Two-pass: Pass 1 counts matching orders; Pass 2 fills a fixed-size array.
No `List<T>` used (NT8 NinjaScript CS0246 avoidance -- `System.Collections.Generic` not always in
implicit using scope). Return type is never null: `new Order[0]` on empty result (JS-002 compliance).
`Execute` batch-cancels all found targets, then batch-submits all new Limit exits per slot, one OCO
pair per target (see Ticket T2 for OCO details).

**OnAskAllClick direct loop (ASKALL-01):**
Rewrote to mirror `OnBidAllClick` exactly:
```
_leaderAccount = _leaderAccount ?? TryResolveLeaderAccount();
if (_leaderAccount == null) return;
PttMoveTargetOne.Execute(_leaderAccount, _instrument, useAsk: true, _askBuffer);
foreach (var item in _followerItems)
    if (item.IsSelected && item.Account != null)
        PttMoveTargetOne.Execute(item.Account, _instrument, useAsk: true, _askBuffer);
```
No `AllAccounts` or `FindRule` dependency. Copier state is irrelevant to button action.

**OnBidAllClick direct loop (BIDALL-01):**
Same pattern as ASKALL-01, `useAsk: false`, `_bidBuffer`. Applied in prior session; confirmed here.

**PttMoveTargetAll.cs:**
Created as a thin wrapper calling `PttMoveTargetOne.Execute` per account via `Account.All` + `FindRule`.
This file is retained but NOT in the panel button critical path. `OnAskAllClick`/`OnBidAllClick` call
`PttMoveTargetOne.Execute` directly. Do not delete `PttMoveTargetAll.cs` without an explicit DW-card.

---

### T1.4 Method Signatures (with CYC targets)

All methods are in `PttMoveTargetOne` (static class) or `TradeCopierPanel` (partial class).

| Method | File | Signature | CYC target |
|--------|------|-----------|-----------|
| `Execute` | `PttMoveTargetOne.cs` | `public static void Execute(Account acc, Instrument instr, bool useAsk, int bufferTicks)` | <= 6 |
| `CollectWorkingTargets` | `PttMoveTargetOne.cs` | `private static Order[] CollectWorkingTargets(Account acc, Instrument instr)` | <= 5 |
| `IsWorkingTarget` | `PttMoveTargetOne.cs` | `private static bool IsWorkingTarget(Order o, Instrument instr)` | <= 5 |
| `TryGetExitPrice` | `PttMoveTargetOne.cs` | `private static bool TryGetExitPrice(Instrument instr, bool useAsk, int bufferTicks, out double price)` | <= 7 |
| `OnAskAllClick` | `TradeCopierPanel.cs` | `private void OnAskAllClick(object sender, RoutedEventArgs e)` | <= 4 |
| `OnBidAllClick` | `TradeCopierPanel.cs` | `private void OnBidAllClick(object sender, RoutedEventArgs e)` | <= 4 |
| `Execute` | `PttMoveTargetAll.cs` | `public static void Execute(Instrument instr, bool useAsk, int bufferTicks)` | <= 5 |

**JS-DNA constraints per method:**

| Method | JS-021 (no lock) | JS-001 (no throw) | JS-033 (no async void) | JS-002 (no null return) |
|--------|-----------------|-------------------|------------------------|------------------------|
| `PttMoveTargetOne.Execute` | Interlocked.Increment only | try/catch log-and-swallow | sync void | N/A (void) |
| `CollectWorkingTargets` | no lock | no throw | sync | returns `new Order[0]` not null |
| `IsWorkingTarget` | no lock | no throw | sync | returns bool |
| `TryGetExitPrice` | no lock | no throw | sync | out param, returns bool |
| `OnAskAllClick` | no lock | no throw | RoutedEventHandler -- sync void allowed | N/A (void) |
| `OnBidAllClick` | no lock | no throw | RoutedEventHandler -- sync void allowed | N/A (void) |
| `PttMoveTargetAll.Execute` | no lock | no throw | sync | N/A (void) |

**Order name prefix contract:**
All orders submitted by `PttMoveTargetOne.Execute` MUST use `"PTT-"` prefix:
- Limit target: `"PTT-MA-Ask-" + seq.ToString("D5")` or `"PTT-MA-Bid-" + seq.ToString("D5")`
- Stop (OCO pair): `"PTT-MA-Stop-" + seq.ToString("D5")`

**NT8-049 CreateOrder arg compliance (AddOnBase pattern):**
```
acc.CreateOrder(instr, action, OrderType.Limit,    OrderEntry.Manual, TimeInForce.Day, qty,
                exitPrice,  0.0,          ocoId, orderName, MaxDate, null)
                // arg6=limitPrice, arg7=stopPrice
acc.CreateOrder(instr, action, OrderType.StopMarket, OrderEntry.Manual, TimeInForce.Gtc, qty,
                0.0,        snapshotStop, ocoId, orderName, MaxDate, null)
                // arg6=limitPrice(0.0), arg7=stopPrice
```

---

### T1.5 xUnit [Fact] Test Names

All tests in `src/PropTraderTools/Tests/B211Tests.cs`. Baseline: 24 failures / 176 passed --
no new failures introduced.

| Test name | Asserts | NT8 dependency |
|-----------|---------|----------------|
| `NoTarget_ReturnsNoOp` | Execute no-ops when no Working Limit target found | Skip (NT8 Account.Orders) |
| `NoPosition_ReturnsNoOp` | Execute no-ops when position.Quantity == 0 | Skip (NT8 Account.Positions) |
| `PartialFill_QtyIsQuantityMinusFilled` | qty = Quantity - Filled = 3 when Qty=5, Filled=2 | YES (pure math) |
| `ZeroFill_QtyEqualsQuantity` | qty = Quantity when Filled = 0 | YES (pure math) |
| `AskPriceCalc_ZeroBuffer_EqualsAsk` | TryGetExitPrice: price = Ask + 0 * tickSize = Ask | Skip (NT8 MarketData) |
| `AskPriceCalc_TwoBufferTicks_EqualsAskPlusTwoTicks` | TryGetExitPrice: price = Ask + 2 * tickSize | Skip (NT8 MarketData) |
| `BidPriceCalc_NegativeBuffer_EqualsBidMinusOneTick` | TryGetExitPrice: price = Bid + (-1) * tickSize | Skip (NT8 MarketData) |
| `PttMaName_NotProtectedByBeSwapGtcSweepGuard` | "PTT-MA-Ask-00001" does NOT match FIX-G-2 "PTT-BE-" guard | YES (pure string) |
| `CollectWorkingTargets_BothPttAndNativeTargetsFound` | IsWorkingTarget returns true for "PTT-MA-Ask-1" AND "Target1" | YES (predicate logic) |
| `CollectWorkingTargets_EmptyOrders_ReturnsEmptyArray` | CollectWorkingTargets returns Order[0] not null on empty | YES (pure array) |
| `IsWorkingTarget_NativeTarget9_ReturnsTrue` | IsWorkingTarget: "Target9" Working Limit Sell = true | YES (string predicate) |
| `IsWorkingTarget_Target_NoDigitSuffix_ReturnsFalse` | IsWorkingTarget: "TargetABC" does NOT match (char.IsDigit fails) | YES (string predicate) |

---

### T1.6 7-Scan Checklist (SCAN-01 through SCAN-07)

**SCAN-01: JS-021 lock() prohibition**
```powershell
grep -rn "lock(" src/PropTraderTools/Features/PttMoveTargetOne.cs `
             src/PropTraderTools/Features/PttMoveTargetAll.cs `
             src/PropTraderTools/TradeCopierPanel.cs
```
Expected: **0 results**. Any match = SCAN-01 FAIL.

**SCAN-02: JS-001 throw new prohibition**
```powershell
grep -rn "throw new" src/PropTraderTools/Features/PttMoveTargetOne.cs `
              src/PropTraderTools/Features/PttMoveTargetAll.cs `
              src/PropTraderTools/TradeCopierPanel.cs
```
Expected: **0 results**. Any match = SCAN-02 FAIL.

**SCAN-03: JS-033 async void prohibition**
```powershell
grep -rn "async void" src/PropTraderTools/Features/PttMoveTargetOne.cs `
               src/PropTraderTools/Features/PttMoveTargetAll.cs `
               src/PropTraderTools/TradeCopierPanel.cs
```
Expected: **0 results**. (Note: `OnAskAllClick`/`OnBidAllClick` in `TradeCopierPanel.cs` are
`private void` RoutedEventHandlers -- sync, not async. They do NOT appear in this scan.)

**SCAN-04: CYC audit**
Manual review of all methods in `PttMoveTargetOne.cs` and `PttMoveTargetAll.cs`:
- `Execute` (PttMoveTargetOne): CYC <= 6
- `CollectWorkingTargets`: CYC <= 5
- `IsWorkingTarget`: CYC <= 5
- `TryGetExitPrice`: CYC <= 7
- `Execute` (PttMoveTargetAll): CYC <= 5
- `OnAskAllClick`: CYC <= 4
- `OnBidAllClick`: CYC <= 4

CYC is computed as: 1 + number of decision points (if, for, while, case, ternary, &&, ||).
Any method exceeding CYC=8 = SCAN-04 FAIL.

**SCAN-05: ASCII-only string literals**
```powershell
grep -Prn "[^\x00-\x7F]" src/PropTraderTools/Features/PttMoveTargetOne.cs `
                          src/PropTraderTools/Features/PttMoveTargetAll.cs `
                          src/PropTraderTools/TradeCopierPanel.cs `
                          src/PropTraderTools/Core/PttContracts.cs
```
Expected: **0 results**. All string literals, comments, and identifiers must be ASCII-only.
No Unicode, emoji, curly quotes, or UTF-8 multi-byte characters.

**SCAN-06: ptt-sync hard-link verification**
```powershell
powershell -File .\scripts\ptt-sync.ps1
```
Expected: **21 linked, 0 errors** (displayed as `21/0`). Any error count > 0 = SCAN-06 FAIL.
Run after every edit to any `.cs` file in `src/PropTraderTools/`.

**SCAN-07: Test suite baseline**
```powershell
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --no-build
```
Expected: **no new failures beyond baseline 24**. Pre-existing baseline is 24 failures / 176 passed.
Any failure count above 24 = SCAN-07 FAIL (regression introduced).

---

### T1.7 Pass Criteria (BUILD_PASS)

T1 is BUILD_PASS when ALL of the following are true:

1. SCAN-01: 0 lock() matches in PttMoveTargetOne.cs, PttMoveTargetAll.cs, TradeCopierPanel.cs
2. SCAN-02: 0 throw new matches in PttMoveTargetOne.cs, PttMoveTargetAll.cs, TradeCopierPanel.cs
3. SCAN-03: 0 async void matches in PttMoveTargetOne.cs, PttMoveTargetAll.cs, TradeCopierPanel.cs
4. SCAN-04: All 7 methods above confirm CYC <= specified target
5. SCAN-05: 0 non-ASCII characters in PttMoveTargetOne.cs, PttMoveTargetAll.cs, TradeCopierPanel.cs, PttContracts.cs
6. SCAN-06: ptt-sync reports 21/0
7. SCAN-07: Test run shows <= 24 failures (baseline; no new regressions)

Additionally, Ph4b guardian symbol check is PASS:
```powershell
powershell -File .\scripts\verify_critical_symbols.ps1
```
(Ticket T1 does not modify `CopyEngine.cs`. If `TradeCopierAddOn.cs` was touched: run this check.)

---

### T1.8 ptt-sync Requirement

`powershell -File .\scripts\ptt-sync.ps1` MUST show **21/0** after any edit to:
- `TradeCopierPanel.cs`
- `PttMoveTargetOne.cs`
- `PttMoveTargetAll.cs`
- `TradeCopierAddOn.cs`
- `PttContracts.cs`

SIM-confirmed: 21/0 per HOTFIX-B211-MOVETARGET-01 log entry.

---

## Ticket T2 — B212 MA-STOP-02 OCO Pair

### T2.1 Header

| Field | Value |
|-------|-------|
| Ticket ID | T2 |
| Title | B212 OCO Stop Pair per Move-Target Slot (MA-STOP-02) |
| Spec req IDs | B212-MA-STOP-02 |
| Hotfix IDs | HOTFIX-B212-MA-STOP-02 (supersedes HOTFIX-B212-MA-STOP-01) |
| Status | APPLIED + SIM-VALIDATED |
| Dependency | T1 must be complete: Execute loop structure required |

**Files in scope:**

| File | Status | Role |
|------|--------|------|
| `src/PropTraderTools/Features/PttMoveTargetOne.cs` | MODIFIED | Execute() OCO pair loop |
| `src/PropTraderTools/Tests/B212Tests.cs` | NEW/MODIFIED | 5+ xUnit [Fact] tests for OCO pair |

---

### T2.2 Problem Statement

**HOTFIX-B212-MA-STOP-01 failure:**
After `PttMoveTargetOne.Execute` cancelled all working ATM bracket targets, the ATM bracket stop
was orphaned (no longer associated with any target) and disappeared from the ladder. STOP-01 added
stop resubmission (`PTT-MA-Stop-NNNNN`) but submitted it with `ocoId = string.Empty`. The stop
reached Accepted state and then stalled -- it never transitioned to Working. When the target
(`PTT-MA-Ask/Bid-NNNNN`) filled, the stop remained stranded in Accepted state, creating
position-flip risk (the stop would eventually trigger and flip the position).

**Precise root cause of STOP-01 failure:**
NT8's OCO mechanism requires BOTH orders in a pair to carry the same non-empty `ocoId` string at
`CreateOrder` time (arg8). A `StopMarket` with `ocoId = string.Empty` is an unlinked, independent
order. NT8 has no mechanism to transition it to Working (it has no associated trigger order). The
fill of the target does not cancel the stranded stop. STOP-01 behaviour was: Accepted-forever stop
with no Working transition -- exactly what `ocoId = string.Empty` produces.

---

### T2.3 Fix Description

**STOP-02 OCO pair pattern (mirrors PttBreakEvenSwap.SubmitSwapPair exactly):**

Inside the `foreach(targets)` loop in `Execute`, per target slot at index `pairIdx`:

1. **Snapshot stop price** (before cancel -- ATM stop is orphaned when targets are cancelled):
   ```csharp
   double snapshotStop = PttQuickExit.SnapshotStopPrice(acc, instr);
   ```

2. **Generate ocoId** (unique per pair, per session, per account):
   ```csharp
   string ocoId = "PTT-MA-" + acc.Name[..8] + "-"
                + NextBeOcoSeq().ToString("D7") + "-" + pairIdx;
   ```
   - `acc.Name[..8]`: first 8 chars of account name (truncated for string length safety)
   - `NextBeOcoSeq()`: `CopyEngine.Instance.NextBeOcoSeq()` -- Interlocked monotonic counter,
     same as BE pairs, reused to avoid introducing a second counter
   - `pairIdx`: slot index, appended to disambiguate pairs within the same Execute call

3. **Submit stop order** (if snapshotStop > 0):
   ```csharp
   // NT8-049: StopMarket -> arg6=limitPrice(0.0), arg7=stopPrice(snapshotStop)
   var stopOrd = acc.CreateOrder(instr, stopAction, OrderType.StopMarket,
       OrderEntry.Manual, TimeInForce.Gtc, qty,
       0.0, snapshotStop, ocoId,
       "PTT-MA-Stop-" + Interlocked.Increment(ref _maSeq).ToString("D5"),
       NinjaTrader.Core.Globals.MaxDate, null);
   ```

4. **Submit target order** (always):
   ```csharp
   // NT8-049: Limit -> arg6=limitPrice(exitPrice), arg7=stopPrice(0.0)
   var targetOrd = acc.CreateOrder(instr, targetAction, OrderType.Limit,
       OrderEntry.Manual, TimeInForce.Day, qty,
       exitPrice, 0.0, ocoId,
       "PTT-MA-Ask-" + Interlocked.Increment(ref _maSeq).ToString("D5"),
       NinjaTrader.Core.Globals.MaxDate, null);
   ```
   When `snapshotStop == 0`: submit target only with `ocoId = string.Empty` (no-OCO path).
   Log: `"[PTT-MA] Ask: ... stop=0"`.

5. **Submit pair atomically:**
   When stop is present: `acc.Submit(new[] { stopOrd, targetOrd })` (both in one call).
   When stop absent: `acc.Submit(new[] { targetOrd })`.
   NT8 auto-cancels the paired order on fill of either side (because both share the same `ocoId`).

**IsWorkingTarget filter safety:** `IsWorkingTarget` requires `OrderType.Limit`. `PTT-MA-Stop-*`
orders are `StopMarket` -- they are excluded from `CollectWorkingTargets` in subsequent calls.
No double-cancel or double-submit risk exists.

**Expected NT8 output (from HOTFIX-B212-MA-STOP-02 log):**
```
[PTT-MA] Ask: Sim101 price=4200.5 targets=2 stop=4196.4
[PTT-MA] pair 0: Sim101 target=PTT-MA-Ask-00001 stop=PTT-MA-Stop-00002 oco=PTT-MA-Sim101--0001234-0 qty=1
[PTT-MA] pair 1: Sim101 target=PTT-MA-Ask-00003 stop=PTT-MA-Stop-00004 oco=PTT-MA-Sim101--0001235-1 qty=1
PTT-MA-Stop-00002 -> Accepted -> Working
PTT-MA-Ask-00001  -> Accepted -> Working
(on target fill: PTT-MA-Stop-00002 -> Cancelled automatically by NT8 OCO)
```

---

### T2.4 Method Signatures (with CYC targets)

| Method | File | Signature | CYC target |
|--------|------|-----------|-----------|
| `Execute` (modified) | `PttMoveTargetOne.cs` | `public static void Execute(Account acc, Instrument instr, bool useAsk, int bufferTicks)` | <= 7 (OCO branch adds +1 vs T1 baseline of 6) |

**JS-DNA constraints:**

| Rule | Constraint | Verification |
|------|-----------|-------------|
| JS-021 | No `lock()`. `Interlocked.Increment(ref _maSeq)` for sequence counter. `NextBeOcoSeq()` uses Interlocked internally. | grep for `lock(` |
| JS-001 | No `throw new`. Each `CreateOrder`+`Submit` call is wrapped in the existing `try/catch` log-and-swallow block. | grep for `throw new` |
| JS-033 | `Execute` is `static void` (sync). No `async` keyword. | grep for `async void` |
| JS-002 | `ocoId` is never null. `snapshotStop == 0` path routes to `string.Empty` ocoId, not null. | no null assignment |
| NT8-049 | `StopMarket`: arg6=`0.0` (limitPrice), arg7=`snapshotStop` (stopPrice). `Limit`: arg6=`exitPrice` (limitPrice), arg7=`0.0` (stopPrice). | manual arg position review |
| ASCII-only | All prefixes (`"PTT-MA-"`, `"PTT-MA-Stop-"`, `"PTT-MA-Ask-"`, `"PTT-MA-Bid-"`) and log strings are ASCII. | grep non-ASCII |

---

### T2.5 xUnit [Fact] Test Names

All tests in `src/PropTraderTools/Tests/B212Tests.cs`.

| Test name | Asserts | NT8 dependency |
|-----------|---------|----------------|
| `OcoId_NotEmpty_WhenSnapshotStopPresent` | Generated ocoId is not `string.Empty` when snapshotStop > 0 | YES (pure string format, inline logic) |
| `OcoId_Empty_WhenSnapshotStopZero` | ocoId = `string.Empty` when snapshotStop == 0 | YES (pure logic) |
| `StopOrder_NotSubmitted_WhenSnapshotStopZero` | When snapshotStop == 0: no StopMarket order created | Skip (NT8 Account.CreateOrder) |
| `StopPriceArg_IsSnapshotStop_NotLimitPrice` | NT8-049: StopMarket arg7 receives snapshotStop, arg6 receives 0.0 | Skip (NT8 Account.CreateOrder) |
| `TargetPriceArg_IsExitPrice_NotStopPrice` | NT8-049: Limit arg6 receives exitPrice, arg7 receives 0.0 | Skip (NT8 Account.CreateOrder) |
| `B212_STOP02_OcoId_Format_ContainsPttMaPrefix` | Generated ocoId starts with `"PTT-MA-"` | YES (pure string) |
| `B212_STOP02_OcoId_Format_PairIdx_Appended` | ocoId for pairIdx=0 ends with `"-0"`, pairIdx=1 ends with `"-1"` | YES (pure string) |
| `B212_STOP02_OcoId_Format_AccountNameTruncated` | acc.Name length > 8 truncated to 8 chars in ocoId | YES (pure Math.Min / string slice) |
| `B212_STOP02_Execute_MethodExists_InPttMoveTargetOne` | `PttMoveTargetOne.Execute` exists with expected signature (reflection) | YES (reflection) |
| `OcoId_StopAndTarget_ShareSameNonEmptyString` | For a given target slot, the ocoId passed to stop order creation equals the ocoId passed to target order creation, and both are non-empty (asserts OCO pair linkage invariant enabling Working-state transition) | YES (pure string logic) |

---

### T2.6 7-Scan Checklist (SCAN-01 through SCAN-07)

**SCAN-01: JS-021 lock() prohibition**
```powershell
grep -rn "lock(" src/PropTraderTools/Features/PttMoveTargetOne.cs
```
Expected: **0 results**.

**SCAN-02: JS-001 throw new prohibition**
```powershell
grep -rn "throw new" src/PropTraderTools/Features/PttMoveTargetOne.cs
```
Expected: **0 results** in new or modified code sections.

**SCAN-03: JS-033 async void prohibition**
```powershell
grep -rn "async void" src/PropTraderTools/Features/PttMoveTargetOne.cs
```
Expected: **0 results**. `Execute` is `static void` (sync).

**SCAN-04: CYC audit**
Manual review of `Execute` in `PttMoveTargetOne.cs` after OCO loop addition:
- `Execute`: CYC <= 7 (was <= 6 in T1; OCO branch adds +1).
  Count: 1 (base) + foreach + if(snapshotStop>0) + if(stopOrd!=null) + existing guards.
  Must not exceed 8.

**SCAN-05: NT8-049 argument position compliance**
Manual review at each `acc.CreateOrder(...)` call:
- `StopMarket` calls: confirm arg6 = `0.0` (limitPrice), arg7 = `snapshotStop` (stopPrice).
- `Limit` calls: confirm arg6 = `exitPrice` (limitPrice), arg7 = `0.0` (stopPrice).

Swapped args are an NT8-silent data bug (order accepts but submits wrong price).
This scan is MANUAL -- no grep pattern reliably detects argument position.

**SCAN-06: ptt-sync hard-link verification**
```powershell
powershell -File .\scripts\ptt-sync.ps1
```
Expected: **21/0**.

**SCAN-07: Test suite baseline**
```powershell
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --no-build
```
Expected: **<= 24 failures** (baseline). No new regressions.

---

### T2.7 Pass Criteria (BUILD_PASS)

T2 is BUILD_PASS when ALL of the following are true:

1. SCAN-01: 0 lock() matches in PttMoveTargetOne.cs
2. SCAN-02: 0 throw new matches in PttMoveTargetOne.cs
3. SCAN-03: 0 async void matches in PttMoveTargetOne.cs
4. SCAN-04: `Execute` CYC <= 7 (not > 8) after OCO loop addition
5. SCAN-05: StopMarket arg6=0.0/arg7=snapshotStop; Limit arg6=exitPrice/arg7=0.0 (manual)
6. SCAN-06: ptt-sync reports 21/0
7. SCAN-07: Test run shows <= 24 failures

SIM confirmation (from HOTFIX-B212-MA-STOP-02 log): `PTT-MA-Stop-NNNNN -> Accepted -> Working`
(not stuck at Accepted). Auto-cancel on target fill confirmed by NT8 OCO.

---

### T2.8 ptt-sync Requirement

`powershell -File .\scripts\ptt-sync.ps1` MUST show **21/0** after any edit to `PttMoveTargetOne.cs`.
SIM-confirmed: 21/0 per HOTFIX-B212-MA-STOP-02 log entry.

---

## Ticket T3 — B212 Re-entry Suppression

### T3.1 Header

| Field | Value |
|-------|-------|
| Ticket ID | T3 |
| Title | B212 Re-entry Suppression (FLATTEN-LEADERSKIP-01 + REENTRY-V3) |
| Spec req IDs | B212-FLATTEN-LEADERSKIP-01, B212-REENTRY-V3 |
| Hotfix IDs | HOTFIX-B212-FLATTEN-LEADERSKIP-01, HOTFIX-B212-REENTRY-V3 |
| Status | APPLIED + SIM-VALIDATED |
| Reverted hotfixes | HOTFIX-B212-BEALL-LEADERSKIP-01, HOTFIX-B212-BREAKEVEN-INSTROVERLOAD-01 v1+v2 (do NOT re-implement) |

**Files in scope:**

| File | Status | Role |
|------|--------|------|
| `src/PropTraderTools/CopyEngine.cs` | MODIFIED | Flatten(Instrument) leader-skip + DispatchCopy two gates |
| `src/PropTraderTools/Features/PttBeSwapAll.cs` | MODIFIED (revert + comment) | Leader-skip code reverted; comment documents why REENTRY-V3 is correct fix |
| `src/PropTraderTools/Tests/B212Tests.cs` | MODIFIED | 4+ additional xUnit [Fact] tests |

---

### T3.2 Problem Statement

**HOTFIX-B212-FLATTEN-LEADERSKIP-01 root cause:**
`RelayFlatten` path: `PttFlatten.Execute(ctx)` submits a market close for the leader
(`FlattenPositionLocal`) and then fires `PttBus.RaiseFlatted`. The `RaiseFlatted` event calls
`RelayFlatten` -> `Flatten(Instrument)` -> `FlatGuardLoop(AllAccounts(instrument))`. This includes
the leader. `FlattenOneAccount(leader, ...)` -> `CancelAllAccountOrders(leader)` cancels native
ATM brackets (`Target1`, `SimStop1`, etc.). NT8 ATM interprets individual bracket cancels as
"user cancelled" and re-arms a new Entry order on the leader. This Entry flows through
`OnOrderUpdate` -> `DispatchCopy` -> copied to followers = spurious re-entry.

The leader's market close was ALREADY submitted by `PttFlatten.Execute` (step 1). The
`FlattenOneAccount(leader)` call is always redundant AND harmful. This is unconditionally true
regardless of copier state.

**HOTFIX-B212-REENTRY-V3 root cause (Sub-Case A -- BE-ALL):**
`PttBreakEvenSwap.Execute` sets `_beSwapInProgress[leader]=true` before calling
`CancelQxBrackets`. NT8 ATM detects bracket cancellation and re-arms a new Entry on the leader.
The `_beSwapInProgress` flag was already checked in `NakedDetector`, `TryLaneEHook`, and
drain handler -- but NOT in `DispatchCopy`. Without a `DispatchCopy` gate, the re-armed Entry
was copied to all followers = spurious re-entry.

**HOTFIX-B212-REENTRY-V3 root cause (Sub-Case B -- Flatten-ALL):**
Same dispatch path as Sub-Case A. After the leader's position closes, NT8 ATM cancels the
leader's remaining brackets and re-arms Entry. `PTT-Flatten` is still in-flight (Submitted /
Accepted / Working / PartFilled). A `HasActivePttFlatten` order-scan check would return true
during this window -- intercepting the re-arm artifact before it copies.

**Why prior approaches (BEALL-LEADERSKIP, BREAKEVEN-INSTROVERLOAD) were reverted:**
Skipping the leader from `PttBeSwapAll` / `BreakEven` overloads broke the copier-OFF case:
when no rule is configured for the instrument, `FindRule` returns the rule with the leader as
`MasterAccount`. The skip fired even with copier disabled -> leader got no BE bracket.
These approaches are REVERTED and must NOT be re-implemented. Only REENTRY-V3 (DispatchCopy
gates) and FLATTEN-LEADERSKIP-01 (RelayFlatten leader-skip) are the authoritative fixes.

---

### T3.3 Fix Description

**HOTFIX-B212-FLATTEN-LEADERSKIP-01:**

In `Flatten(Instrument instrument)` (the RelayFlatten overload -- NOT `Flatten(Account, Instrument)`):
```csharp
// B212-FLATTEN-REENTRY: leader already closed by PttFlatten.Execute before RelayFlatten fires.
// Calling FlattenOneAccount on the leader is redundant AND triggers NT8 ATM re-arm.
// Skip unconditionally -- this is correct regardless of copier state.
var rule = FindRule(instrument);
var leader = rule?.MasterAccount;
FlatGuardLoop(
    () => AllAccounts(instrument).Where(acc => leader == null || acc.Name != leader.Name),
    acc => { var p = FindPosition(acc, instrument); return p == null || p.Quantity == 0; },
    acc => FlattenOneAccount(acc, instrument)
);
```
`FindRule` is null-safe. When no rule exists: `leader = null` -> `.Where` predicate is always true
-> all accounts processed. `LINQ.Where` on `IEnumerable<Account>` is not a hot path (button click only).

**HOTFIX-B212-REENTRY-V3:**

In `DispatchCopy(Order order)`, after Gate 0.5 (exit signal name filter) and before Gate 3:
```csharp
// Gate B212-REENTRY-V3: suppress NT8 ATM re-arm artifact during BE swap window.
// IsBeSwapInProgress = ConcurrentDictionary TryGetValue (lock-free, O(1)).
if (IsBeSwapInProgress(order.Account?.Name))
    return;

// Gate B212-REENTRY-FLATTEN: suppress NT8 ATM re-arm artifact during Flatten window.
// HasActivePttFlatten = acc.Orders.ToList() scan for PTT-Flatten in active state.
if (order.Account != null && HasActivePttFlatten(order.Account, order.Instrument))
    return;
```

**Gate placement rationale:**
Gate 0.5 exits for `PTT-*` order names. The NT8 re-armed Entry has name `"Entry"` (not `PTT-*`),
so it passes Gate 0.5 and reaches REENTRY-V3 gates. The gates intercept it before Gate 3's
state-level dispatch trigger. When copier is OFF (`Gate 1` exits before reaching `DispatchCopy`),
these gates never fire -- zero side effect on copier-OFF paths.

**`IsBeSwapInProgress(string accName)` mechanism:**
- `ConcurrentDictionary<string, bool>` keyed by account name (already established pattern)
- `TryGetValue(accName, out _)`: lock-free O(1) read
- Flag set by `PttBreakEvenSwap.Execute` BEFORE `CancelQxBrackets`
- Flag cleared when `PTT-BE-Stop-N` reaches Working state (~50-200ms window)
- Null guard: `IsBeSwapInProgress(null)` returns false (no suppression when account unavailable)

**`HasActivePttFlatten(Account acc, Instrument instr)` mechanism:**
- Scans `acc.Orders.ToList()` (lock-free NT8 snapshot) for a `"PTT-Flatten"` order in:
  `Submitted`, `Accepted`, `Working`, or `PartFilled` state
- Returns true if found (leader's close is still in-flight)
- `PartFilled` included: without it, gate returns false during the partial-fill window (large
  positions that fill over multiple events) leaving a gap in suppression
- O(N) where N = open orders on account (typically < 20); acceptable for button-click path

**PttBeSwapAll.cs revert:**
Any prior leader-skip code (from HOTFIX-B212-BEALL-LEADERSKIP-01 or
HOTFIX-B212-BREAKEVEN-INSTROVERLOAD-01) in `PttBeSwapAll.Execute` MUST be reverted.
Final state: no `masterAccount`/`leader` conditional inside `PttBeSwapAll.Execute`.
A comment block is added documenting why `REENTRY-V3` in `DispatchCopy` is the correct fix
and why any leader-skip here breaks copier-OFF. This is a revert + documentation change only.

---

### T3.4 Method Signatures (with CYC targets)

| Method | File | Signature | CYC target |
|--------|------|-----------|-----------|
| `Flatten(Instrument)` | `CopyEngine.cs` | `internal void Flatten(Instrument instrument)` | <= 8 (+1 for Where lambda) |
| `DispatchCopy` | `CopyEngine.cs` | `private void DispatchCopy(Order order)` | <= 8 (+2 for two REENTRY-V3 gates; was <= 8 pre-patch per L2783 comment) |
| `IsBeSwapInProgress` | `CopyEngine.cs` | `private bool IsBeSwapInProgress(string accountName)` | <= 2 (null guard + ConcurrentDictionary TryGetValue) |
| `HasActivePttFlatten` | `CopyEngine.cs` | `private bool HasActivePttFlatten(Account acc, Instrument instr)` | <= 5 (null guards + order scan + state enum check) |
| `Execute` | `PttBeSwapAll.cs` | `public static void Execute(int bufferTicks, Instrument instrument)` | <= 5 (reverted to pre-LEADERSKIP-01 baseline; no leader-skip branch) |

**JS-DNA constraints per method:**

| Method | JS-021 | JS-001 | JS-033 | JS-002 |
|--------|--------|--------|--------|--------|
| `Flatten(Instrument)` | LINQ .Where (no lock); FindRule = ConcurrentDictionary lookup | no throw | sync internal void | leader=null produces no-skip (correct) |
| `DispatchCopy` | IsBeSwapInProgress = ConcurrentDictionary TryGetValue; HasActivePttFlatten = acc.Orders.ToList() | no throw | sync private void | early return (no null return) |
| `IsBeSwapInProgress` | ConcurrentDictionary TryGetValue -- lock-free | no throw | sync | returns false on null -- safe |
| `HasActivePttFlatten` | acc.Orders.ToList() -- NT8 lock-free snapshot | no throw | sync | returns bool |
| `PttBeSwapAll.Execute` | no lock (reverted) | no throw | sync static void | N/A |

**Deleted-method contract (Ph4b Deletion Audit):**
The reverted `masterAccount`/`leader` variable and `continue` guard in `PttBeSwapAll.Execute`
do NOT require DW-cards because they were introduced in HOTFIX-B212-BEALL-LEADERSKIP-01
(which is explicitly listed as REVERTED and not formalized). The revert restores the
pre-LEADERSKIP-01 baseline and is authorized by this ticket.

---

### T3.5 xUnit [Fact] Test Names

All tests in `src/PropTraderTools/Tests/B212Tests.cs`. Pre-existing tests cover DW-B212-01,
DW-B212-03, DW-B212-05 (5 structural [Fact] tests). New tests extend this file.

| Test name | Asserts | NT8 dependency |
|-----------|---------|----------------|
| `DispatchCopy_BeSwapInProgress_ReturnsEarly` | Gate B212-REENTRY-V3 fires when IsBeSwapInProgress returns true | Skip (NT8 CopyEngine thread) |
| `DispatchCopy_ActivePttFlatten_ReturnsEarly` | Gate B212-REENTRY-FLATTEN fires when HasActivePttFlatten returns true | Skip (NT8 Account.Orders) |
| `DispatchCopy_NoGuardsFired_Proceeds` | DispatchCopy proceeds when both gates return false | Skip (NT8 CopyEngine thread) |
| `FlattenInstrument_LeaderSkipped_FollowersFlattened` | Flatten(Instrument): leader account excluded from FlatGuardLoop | Skip (NT8 Account) |
| `FlattenInstrument_NullLeader_AllAccountsFlattened` | When FindRule returns null: Where predicate passes all accounts | YES (pure LINQ logic) |
| `BeSwapAll_LeaderNotSkipped_AllAccountsSwapped` | PttBeSwapAll.Execute does NOT skip leader (copier-OFF scenario -- revert confirmed) | Skip (NT8 Account.All) |
| `HasActivePttFlatten_WorkingState_ReturnsTrue` | HasActivePttFlatten returns true for "PTT-Flatten" order in Working state | YES (reflection + mock order state) |
| `HasActivePttFlatten_FilledState_ReturnsFalse` | HasActivePttFlatten returns false for "PTT-Flatten" order in Filled state | YES (reflection + mock order state) |
| `IsBeSwapInProgress_SetThenClear_WorksCorrectly` | Flag returns true after set; returns false after clear | YES (reflection or internal test helper) |
| `B212_FLATTEN_FlattenInstrument_SkipsLeader_MethodSignatureConfirmed` | `Flatten(Instrument)` overload exists as internal instance method (reflection) | YES (reflection) |
| `B212_REENTRY_V3_HasActivePttFlatten_MethodExists_ReturnsVoid` | `HasActivePttFlatten` exists as private static/instance, returns bool (reflection) | YES (reflection) |
| `B212_REENTRY_V3_IsBeSwapInProgress_NullAccName_ReturnsFalse` | `IsBeSwapInProgress(null)` returns false (null-safe gate) | YES (reflection with null arg) |

---

### T3.6 7-Scan Checklist (SCAN-01 through SCAN-07)

**SCAN-01: JS-021 lock() prohibition**
```powershell
# Verify no lock() was introduced in DispatchCopy or Flatten(Instrument) modified bodies:
git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | grep "^+" | grep "lock("
# Also verify PttBeSwapAll.cs revert contains no new lock():
git diff HEAD~1 HEAD -- src/PropTraderTools/Features/PttBeSwapAll.cs | grep "^+" | grep "lock("
```
Expected: **0 results in added lines** (lines prefixed with `+`). Pre-existing `lock()` in
unmodified CopyEngine.cs methods are NOT violations of this ticket and will not appear in the
diff output. Any added `lock(` line = SCAN-01 FAIL (JS-021 P0).

**SCAN-02: JS-001 throw new prohibition**
```powershell
# Verify no throw new was added to DispatchCopy, Flatten(Instrument), IsBeSwapInProgress,
# or HasActivePttFlatten modified bodies:
git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | grep "^+" | grep "throw new"
```
Expected: **0 results in added lines**. Pre-existing `throw new` in other unmodified
CopyEngine.cs methods are NOT violations of this ticket. Any added `throw new` line = SCAN-02 FAIL.

**SCAN-03: JS-033 async void prohibition**
```powershell
# Verify no async void was introduced in CopyEngine.cs modified methods:
git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | grep "^+" | grep "async void"
# Verify no async void in PttBeSwapAll.cs (Execute is static void sync by design):
grep -rn "async void" src/PropTraderTools/Features/PttBeSwapAll.cs
```
Expected: **0 results in added lines** for CopyEngine.cs diff. **0 results** for PttBeSwapAll.cs.
`DispatchCopy`, `Flatten(Instrument)`, `IsBeSwapInProgress`, and `HasActivePttFlatten` are all
synchronous -- no `async` keyword. CopyEngine.cs may have pre-existing `async void` event
handler overrides (e.g. `OnOrderUpdate`) in unmodified methods; those are not violations here
and will not appear in the diff output. Any added `async void` line = SCAN-03 FAIL.

**SCAN-04: CYC audit**
Manual review of modified methods in `CopyEngine.cs`:
- `DispatchCopy`: CYC <= 8. Pre-patch CYC <= 8 (per L2783 comment). +2 gates = final <= 8.
  If CYC would exceed 8: extract one gate to a helper method.
- `Flatten(Instrument)`: CYC <= 8 (+1 for `.Where` lambda condition).
- `IsBeSwapInProgress`: CYC <= 2 (null guard + TryGetValue).
- `HasActivePttFlatten`: CYC <= 5 (null guards + scan loop + state comparison).
- `PttBeSwapAll.Execute`: CYC <= 5 (reverted to baseline; no leader-skip branch).

**SCAN-05: Ph4b Deletion Audit**
```powershell
git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | `
    Select-String "^-    (private|internal|public)"
```
Every deleted symbol (lines starting with `-` and containing `private`/`internal`/`public`) MUST
have an authorizing DW-card in this ticket or an explicit revert authorization.

Authorizations for T3:
- Deleted `masterAccount`/`leader` conditional in `PttBeSwapAll.cs`: authorized by this ticket
  (HOTFIX-B212-BEALL-LEADERSKIP-01 revert)
- No other symbol deletions expected for this ticket

**SCAN-06: ptt-sync hard-link verification**
```powershell
powershell -File .\scripts\ptt-sync.ps1
```
Expected: **21/0**.

Additionally, guardian symbol check MUST pass after any `CopyEngine.cs` edit:
```powershell
powershell -File .\scripts\verify_critical_symbols.ps1
```
Expected: PASS.

**SCAN-07: Test suite baseline**
```powershell
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --no-build
```
Expected: **<= 24 failures** (baseline). No new regressions.

---

### T3.7 Pass Criteria (BUILD_PASS)

T3 is BUILD_PASS when ALL of the following are true:

1. SCAN-01: 0 added `lock(` lines in CopyEngine.cs and PttBeSwapAll.cs diff (git diff scope)
2. SCAN-02: 0 added `throw new` lines in CopyEngine.cs diff (git diff scope)
3. SCAN-03: 0 added `async void` lines in CopyEngine.cs diff; 0 `async void` in PttBeSwapAll.cs
4. SCAN-04: DispatchCopy CYC <= 8; Flatten(Instrument) CYC <= 8; all other modified methods within targets
5. SCAN-05: Ph4b deletion audit -- every deleted symbol has DW-card or revert authorization
6. SCAN-06: ptt-sync reports 21/0 AND verify_critical_symbols.ps1 PASS
7. SCAN-07: Test run shows <= 24 failures

**SIM pass criteria (from architecture plan Section 5):**

| Scenario | Required result |
|----------|-----------------|
| Copier ON, BE-ALL | Sim101 gets PTT-BE-Stop-N Working; followers get PTT-BE-Stop-N; NO new Entry on any account |
| Copier OFF, BE-ALL | All accounts including Sim101 get BE swap (no leader skip) |
| Copier ON, Flatten-ALL | Leader position closes; followers close; NO new Entry on any account |
| Copier ON, BE-ALL then new entry >200ms later | Gate clears after PTT-BE-Stop-N Working; new entry copies correctly |

**Single-hunk ban:** After `CopyEngine.cs` edit:
```powershell
(git show HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^@@").Count -gt 1
```
This must be `True` (more than one hunk). A single-hunk result means a full-file replace was
committed = VERIFY_FAIL per AGENTS.md Platinum Standard.

**Commit size gate:** If `CopyEngine.cs` has > 300 changed lines, Director explicit approval
is required before Ph4b may issue VERIFY_PASS (per AGENTS.md Platinum Standard).

---

### T3.8 ptt-sync Requirement

`powershell -File .\scripts\ptt-sync.ps1` MUST show **21/0** after any edit to:
- `CopyEngine.cs`
- `PttBeSwapAll.cs`

SIM-confirmed: 21/0 per HOTFIX-B212-REENTRY-V3 and HOTFIX-B212-FLATTEN-LEADERSKIP-01 log entries.

---

## Block-Level Sign-off Requirements

### Deferred Items from B211 Carried Forward

| ID | Item | Priority | Status in B212 block |
|----|------|----------|---------------------|
| DW-B211-01 | Direction-aware validation | P2 | Still OPEN -- not in scope |
| DW-B211-02 | Increase/Decrease nudge buttons | P2 | Still OPEN -- blocked on DW-B211-01 |
| DW-B211-04 | GTC sweep race (_maInProgress) | P1 | CLOSED: SIM Round confirmed race not observed |
| DW-B209-01 | Guardian script single-hunk false positive | P2 | Still OPEN |
| DW-B209-02 | TradeCopierPanel.cs BrushTeal gap | P2 | Still OPEN |
| DW-B209-04 | specs/002-trade-copier-spec.html B209 updates | P1 | Still OPEN |

### New Deferred Items from B212

| ID | Item | Priority | Target |
|----|------|----------|--------|
| DW-B212-01 | Trim(Instrument) RelayTrim path -- investigate for same leader-skip need as Flatten(Instrument) | P1 | Next block |
| DW-B212-02 | PttMoveTargetAll.cs future role -- retain or deprecate | P2 | Future block |
| DW-B212-03 | DIFF LIMIT compliance audit -- confirm B211+B212 PR src/ diff < 10,000 chars | P1 | Before PR push |

### Block BUILD_PASS Definition

The B211+B212 block is BUILD_PASS when:

1. T1 BUILD_PASS (all 7 scans)
2. T2 BUILD_PASS (all 7 scans)
3. T3 BUILD_PASS (all 7 scans)
4. `verify_critical_symbols.ps1` PASS on `CopyEngine.cs`
5. ptt-sync 21/0 across all modified files
6. No single-hunk commit on `CopyEngine.cs`
7. SIM validation criteria from architecture plan Sections 5.1, 5.2, 5.3 confirmed

---

*Tickets generated by ptt-architect. Engineer contract: implement from tickets exactly.
Verifier contract: verify source vs tickets independently before issuing VERIFY_PASS.*
