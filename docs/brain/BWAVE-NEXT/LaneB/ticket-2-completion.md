# BWAVE-NEXT Lane B -- Ticket 2 Completion Report

## Header

- **Ticket**: T2 -- DW-NEW-08 Option D: Cancel-Before-Dispatch Drain
- **Engineer**: ptt-engineer
- **Date**: 2026-09-04
- **Status**: BUILD_PASS

---

## New Code Added

### _pendingDispatchDrains field (STEP 3)
- **Location**: line 379 (immediately after `_nakedDetectLastQueuedTicks` at line 373-374)
- **Declaration**: `private readonly ConcurrentDictionary<string, PendingDispatchDrain> _pendingDispatchDrains = new ConcurrentDictionary<string, PendingDispatchDrain>(StringComparer.Ordinal);`
- JS-008 compliant: readonly
- JS-021 compliant: no lock()

### DrainThenDispatch
- **Location**: line 6496
- **CYC**: 4 (branch 1: null guard, branch 2: !Any() fast path, branch 3: foreach loop, branch 4: cancelCount==0 edge guard)
- **Signature**: `private void DrainThenDispatch(Account follower, Instrument instrument, int qty, double price, OrderAction action, OrderType orderType)`
- Uses `Account.Cancel(Order[])` -- AddOnBase available. NO Account.Change(). NO lock().
- Logs `[DRAIN] acct=... cancel-sent=N`

### SubmitEntryDirect
- **Location**: line 6555
- **CYC**: 2 (branch 1: order null guard, branch 2: ternary for limitPx/stopPx)
- **Signature**: `private void SubmitEntryDirect(Account follower, Instrument instrument, int qty, double price, OrderAction action, OrderType orderType)`
- Uses `Account.CreateOrder()` + `Account.Submit()` -- AddOnBase available.
- Logs `[DRAIN-SUBMIT] acct=... instr=... price=N`
- Order name: `"PTT-Copy"` (matches existing HandleEntryChange pattern)

### OnDrainCancelAck
- **Location**: line 6589
- **CYC**: 3 (branch 1: TryGetValue fail early return, branch 2: remaining<0 underflow guard, branch 3: remaining==0 fire)
- **Signature**: `private void OnDrainCancelAck(string acctKey)`
- Synchronous void. NOT async void (JS-033 compliant).
- Uses `Interlocked.Decrement` -- atomic, no lock().
- Logs `[DRAIN-UNDERFLOW]` on underflow.

### SubmitDrainedEntry
- **Location**: line 6608
- **CYC**: 3 (branch 1: TryRemove fail early return, branch 2: FollowerAccount null early return, branch 3: delegated to SubmitEntryDirect)
- **Signature**: `private void SubmitDrainedEntry(string acctKey)`
- Uses `_pendingDispatchDrains.TryRemove` -- atomic. Delegates to SubmitEntryDirect.
- NO Account.Change().

### TryDrainWatchdog
- **Location**: line 6629
- **CYC**: 3 (branch 1: IsEmpty fast-path, branch 2: foreach loop, branch 3: timestamp comparison)
- **Signature**: `private void TryDrainWatchdog()`
- 2000L ms timeout. NO submit on timeout -- log and remove only.
- NO System.Threading.Timer. Fired unconditionally from OnOrderUpdate.
- Logs `[DRAIN-TIMEOUT] acct=...`

### PendingDispatchDrain nested class (STEP 2)
- **Location**: line 6650
- `private sealed class PendingDispatchDrain` with 9 fields:
  - `FollowerAcctKey` (string) -- { get; private set; }
  - `Instrument` (Instrument) -- { get; private set; }
  - `Qty` (int) -- { get; private set; }
  - `Price` (double) -- { get; private set; }
  - `Action` (OrderAction) -- { get; private set; }
  - `OrderType` (OrderType) -- { get; private set; }
  - `FollowerAccount` (Account) -- { get; private set; }
  - `PendingCancelCount` (int, plain field) -- mutable for Interlocked.Decrement ref
  - `TimestampTicks` (long) -- { get; private set; }
- CYC=0 (data class, no logic methods)
- Explicit constructor (NT8-001: no `{ get; init; }`)

---

## Modified Methods

### HandleEntryChange
- **Location**: line 3717
- **Change**: Replaced the cancel+create+submit block (old lines 3738-3756: `acc.Cancel + acc.CreateOrder + acc.Submit + _dedupCache update + StatusUpdate`) with a single `DrainThenDispatch(acc, instrument, fo.Quantity, newPrice, fo.OrderAction, fo.OrderType)` call.
- **CYC before**: 7 (branches 1-7, including order-null guard and StopLimit ternaries)
- **CYC after**: 6 (order-null guard removed -- handled inside DrainThenDispatch/SubmitEntryDirect; StopLimit ternaries moved into SubmitEntryDirect)
- **CYC comment updated**: from CYC=7 with 7-branch list to CYC=6 with 6-branch list + DW-NEW-08-D note.

### OnOrderUpdate
- **Location**: line 1367
- **Change**: Added 2 items between TryNakedDetect(e) and Gate 1 (!_isCopyEnabled):
  1. Drain-ack routing: `if ((OrderState.Cancelled || OrderState.Rejected || OrderState.Filled) && _pendingDispatchDrains.ContainsKey(e.Order.Account.Name)) OnDrainCancelAck(e.Order.Account.Name);` (+1 branch)
  2. Unconditional `TryDrainWatchdog()` call (+0 CYC)
- **CYC before**: 6
- **CYC after**: 7 (6 + 1 drain-ack branch)

---

## Deviation from Ticket Spec

**Print() vs NinjaTrader.Code.Output.Process()**: Ticket spec used `Print(...)` shorthand in pseudocode. The actual NT8 AddOn logging API used throughout CopyEngine.cs is `NinjaTrader.Code.Output.Process(..., NinjaTrader.NinjaScript.PrintTo.OutputTab1)`. All 4 log calls use the correct NT8 API. The log message strings are identical to the spec (`[DRAIN]`, `[DRAIN-SUBMIT]`, `[DRAIN-UNDERFLOW]`, `[DRAIN-TIMEOUT]`). This is a build-required adaptation, not a scope deviation.

---

## 7 Scan Results (Layer 2)

| # | Scan | Command | Result | Verdict |
|---|------|---------|--------|---------|
| SCAN-01 | JS-021 lock() | `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "lock\s*\(" \| Where-Object { $_.Line -notmatch "^\s*//" }` | 0 results | PASS |
| SCAN-02 | JS-033 async void | `Select-String -Path src/PropTraderTools/*.cs -Pattern "async void [A-Z]" \| Where-Object { $_.Line -notmatch "^\s*//" }` | 0 results | PASS |
| SCAN-03 | JS-002 return null (new) | `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "return null"` -- verified 0 in T2 methods (DrainThenDispatch, SubmitEntryDirect, OnDrainCancelAck, SubmitDrainedEntry, TryDrainWatchdog) | 0 new in T2 methods (all hits pre-existing at lines <6490) | PASS |
| SCAN-04 | JS-001 throw new | `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "throw new" \| Where-Object { $_.Line -notmatch "^\s*//" }` | 0 results | PASS |
| SCAN-05 | CYC budget | Manual count: DrainThenDispatch=4, SubmitEntryDirect=2, OnDrainCancelAck=3, SubmitDrainedEntry=3, TryDrainWatchdog=3, HandleEntryChange=6, OnOrderUpdate=7 | All <=8 | PASS |
| SCAN-06 | ASCII-only | `$bytes = [System.IO.File]::ReadAllBytes("src/PropTraderTools/CopyEngine.cs"); ($bytes \| Where-Object { $_ -gt 0x7F }).Count` | 0 | PASS |
| SCAN-07 | xUnit only | `Select-String -Path src/PropTraderTools/Tests/BwaveNextLaneBTests.cs -Pattern "\[Fact\]\|\[Test\]"` | 3 [Fact] at lines 17, 54, 80 -- 0 [Test] | PASS |

---

## Build Result (verbatim last 10 lines)

```
C:\WSGTA\universal-or-strategy\src\PropTraderTools\Tests\B131Tests.cs(165,13): warning xUnit2004: Do not use
Assert.Equal() to check for boolean conditions. Use Assert.True instead.
(https://xunit.net/xunit.analyzers/rules/xUnit2004)
[C:\WSGTA\universal-or-strategy\src\PropTraderTools\PropTraderTools.csproj]
Build succeeded.
C:\WSGTA\universal-or-strategy\src\PropTraderTools\Tests\B131Tests.cs(165,13): warning xUnit2004: Do not use
Assert.Equal() to check for boolean conditions. Use Assert.True instead.
(https://xunit.net/xunit.analyzers/rules/xUnit2004)
[C:\WSGTA\universal-or-strategy\src\PropTraderTools\PropTraderTools.csproj]
    1 Warning(s)
    0 Error(s)
```

Note: The warning at B131Tests.cs(165) is pre-existing (not introduced by T2).

---

## Test Result (verbatim)

```
Determining projects to restore...
  All projects are up-to-date for restore.
  PropTraderTools -> C:\WSGTA\universal-or-strategy\src\PropTraderTools\bin\Debug\PropTraderTools.dll
Test run for C:\WSGTA\universal-or-strategy\src\PropTraderTools\bin\Debug\PropTraderTools.dll (.NETFramework,Version=v4.8)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:     3, Skipped:     0, Total:     3, Duration: 571 ms - PropTraderTools.dll (net48)
```

---

## NT8 Sync (verbatim last 10 lines)

```
OK       Features\PttCancel.cs
  OK       Features\PttCopier.cs
  OK       Features\PttFlatten.cs
  OK       Features\PttFollowerStrategy.cs
  OK       Features\PttGlobalBreakEven.cs
  OK       Features\PttGlobalQuickExit.cs
  OK       Features\PttQuickExit.cs
  OK       Features\PttTrim.cs

=== SYNC + VERIFY: PASS (18 files confirmed) ===
```

18/18 OK, 0 MISMATCH.

---

## SIM Gate Status

**DEFERRED** -- requires live NT8 with SIM account. Non-blocking for VERIFY_PASS.

SIM gate requires live NT8 SIM session with leader performing 3+ drag-reposition cycles.
Evidence to record:
1. NT8 output log shows [DRAIN] cancel-sent=N before [DRAIN-SUBMIT] on every dispatch cycle.
2. NT8 output log shows [DRAIN-TIMEOUT] if a cancel is unacknowledged for >2s.
3. Under 14+ drag cycles, follower ends every cycle with either flat OR Entry:Filled + brackets (no naked position).

Status: Pending Director-scheduled SIM session.

---

*Report written: 2026-09-04 | ptt-engineer | BWAVE-NEXT Lane B T2*
