# B211 Ticket-1 Completion Report

**Engineer**: ptt-engineer (Phase 4a)  
**Ticket**: TICKET-1 -- Core Logic: New Feature Files + PttContracts Update  
**Date**: 2026-07-07  
**Status**: BUILD_PASS  

---

## Scope: Ticket 1 Only

This session implements ONLY Ticket-1. TradeCopierPanel.cs was NOT touched.  
Ticket-2 must not begin until Ticket-1 VERIFY_PASS is confirmed.

---

## Files Created / Modified

| File | Action | Line Count |
|------|--------|-----------|
| `src/PropTraderTools/Features/PttMoveTargetOne.cs` | NEW | 143 lines |
| `src/PropTraderTools/Features/PttMoveTargetAll.cs` | NEW | 38 lines |
| `src/PropTraderTools/Core/PttContracts.cs` | MODIFY (2 constants added) | 357 lines (was 345) |
| `src/PropTraderTools/Tests/B211Tests.cs` | NEW | 144 lines |

`src/PropTraderTools/Features/` directory already existed (PttBeSwapAll.cs, PttBeSwapOne.cs, etc. present).  

---

## SCAN-01: CYC Scan

Manual branch count (non-strict McCabe -- each `if`/ternary/`??`/`foreach`/`?.` = +1):

| Method | File | Measured CYC | Limit | Result |
|--------|------|-------------|-------|--------|
| PttMoveTargetOne.Execute | PttMoveTargetOne.cs | 6 | 8 | PASS |
| PttMoveTargetOne.TryGetExitPrice | PttMoveTargetOne.cs | **7** | 8 | PASS |
| PttMoveTargetOne.TryFindWorkingTarget | PttMoveTargetOne.cs | 6 | 8 | PASS |
| PttMoveTargetAll.Execute | PttMoveTargetAll.cs | 3 | 8 | PASS |

### CYC Detail

**Execute (CYC=6)**:  
base(1) + `if instr==null`(1) + `if !TryGetExitPrice`(1) + `if pos==null||qty==0`(1) + `if !TryFindWorkingTarget`(1) + `useAsk?:` ternary(1) = 6

**TryGetExitPrice (CYC=7)** -- V5 advisory applies:  
base(1) + `?.` null-conditional(1) + `if tickSize<=0`(1) + `if md==null`(1) + `useAsk?:` ternary(1) + `if side==null`(1) + `if basePrice<=0`(1) = 7  
Plan table stated CYC=6 -- corrected to CYC=7 per V5 advisory (plan-reviewer confirmed within <= 8 limit).

**TryFindWorkingTarget (CYC=6)**:  
base(1) + `foreach`(1) + compound `if ..||..`(1) + `if OrderType!=Limit`(1) + `if name==null||!StartsWith`(1) + `if action!=Sell&&action!=BuyToCover`(1) = 6

**PttMoveTargetAll.Execute (CYC=3)**:  
base(1) + `if instrument==null`(1) + `foreach`(1) = 3

**SCAN-01: PASS**

---

## SCAN-02: lock() Scan

Command:  
```powershell
Select-String -Path "src\PropTraderTools\Features\PttMoveTargetOne.cs","src\PropTraderTools\Features\PttMoveTargetAll.cs","src\PropTraderTools\Core\PttContracts.cs" -Pattern "lock\(" -SimpleMatch
```

Output: (no output -- zero matches)

**SCAN-02: PASS** -- zero `lock(` occurrences in Ticket-1 files.  
`_maSeq` uses `System.Threading.Interlocked.Increment` -- JS-021 compliant.

---

## SCAN-03: async void Scan

Command:  
```powershell
Select-String -Path "src\PropTraderTools\Features\PttMoveTargetOne.cs","src\PropTraderTools\Features\PttMoveTargetAll.cs","src\PropTraderTools\Core\PttContracts.cs" -Pattern "async void " -SimpleMatch
```

Output: (no output -- zero matches)

**SCAN-03: PASS** -- all new methods are synchronous void. No async keyword anywhere.

---

## SCAN-04: null return Scan

Command:  
```powershell
Select-String -Path "src\PropTraderTools\Features\PttMoveTargetOne.cs","src\PropTraderTools\Features\PttMoveTargetAll.cs","src\PropTraderTools\Core\PttContracts.cs" -Pattern "return null" -SimpleMatch
```

Output: (no output -- zero matches)

**SCAN-04: PASS** -- TryGet pattern used throughout. No method returns null.  
`TryFindWorkingTarget` assigns `target = default(NinjaTrader.Cbi.Order)` and returns `bool`.

---

## SCAN-05: throw Scan

Command:  
```powershell
Select-String -Path "src\PropTraderTools\Features\PttMoveTargetOne.cs","src\PropTraderTools\Features\PttMoveTargetAll.cs","src\PropTraderTools\Core\PttContracts.cs" -Pattern "^\s+throw\s"
```

Output: (no output -- zero actual throw statements)

Note: `Select-String -Pattern "throw "` (without line-start anchor) returns 2 hits in PttMoveTargetOne.cs comments containing the phrase "no throw". These are comments only -- confirmed no executable `throw` statement in any new/modified file.

**SCAN-05: PASS** -- NT8 API calls wrapped in try/catch log-and-swallow. No rethrow, no `throw new`.

---

## SCAN-06: ASCII Scan

Manual inventory of all string literals in new/modified files:

| String Literal | File | ASCII? |
|----------------|------|--------|
| `"[PTT-MA] no position: "` | PttMoveTargetOne.cs | PASS |
| `"[PTT-MA] no target found for "` | PttMoveTargetOne.cs | PASS |
| `"[PTT-MA] "` | PttMoveTargetOne.cs | PASS |
| `"[PTT-MA] Execute exception: "` | PttMoveTargetOne.cs | PASS |
| `"D5"` | PttMoveTargetOne.cs | PASS |
| `"PTT-"` | PttMoveTargetOne.cs | PASS |
| `"[PTT-MA-ALL] instrument null"` | PttMoveTargetAll.cs | PASS |
| `"[PTT-MA-ALL] useAsk="` | PttMoveTargetAll.cs | PASS |
| `"PTT-MA-Ask-"` | PttContracts.cs | PASS |
| `"PTT-MA-Bid-"` | PttContracts.cs | PASS |

No Unicode characters. No curly quotes. No em-dashes. All identifiers are ASCII-only.

**SCAN-06: PASS**

---

## SCAN-07: ptt-sync

Command:  
```powershell
powershell -File .\scripts\ptt-sync.ps1
```

Exact output:
```
  LINKED  CopyEngine.cs
  LINKED  FeatureFlags.cs
  LINKED  LicenseClient.cs
  LINKED  PttTickMath.cs
  LINKED  TradeCopierAddOn.cs
  LINKED  TradeCopierPanel.cs
  LINKED  TradeCopierWindow.cs
  LINKED  Core\PttContracts.cs
  LINKED  Features\PttBeSwapAll.cs
  LINKED  Features\PttBeSwapOne.cs
  LINKED  Features\PttBreakEvenSwap.cs
  LINKED  Features\PttCancel.cs
  LINKED  Features\PttCopier.cs
  LINKED  Features\PttFlatten.cs
  LINKED  Features\PttFollowerStrategy.cs
  LINKED  Features\PttGlobalBreakEven.cs
  LINKED  Features\PttGlobalQuickExit.cs
  LINKED  Features\PttMoveTargetAll.cs
  LINKED  Features\PttMoveTargetOne.cs
  LINKED  Features\PttQuickExit.cs
  LINKED  Features\PttTrim.cs

ptt-sync complete: 21 linked, 0 errors
```

**SCAN-07: PASS** -- 21/0. Both new Feature files (`PttMoveTargetOne.cs`, `PttMoveTargetAll.cs`) are confirmed linked.

---

## V5 Advisory Confirmation

**TryGetExitPrice CYC = 7** (not 6).  
Plan table stated CYC=6; plan-reviewer V5 advisory corrected to CYC=7 in ticket SCAN-01 note.  
CYC=7 is within the global limit of 8. No redesign required.  
Recorded here per ticket requirement.

---

## xUnit Tests Confirmed

All 8 required [Fact] tests present in `B211Tests.cs`:

| # | Test Name | [Fact]? | xUnit? |
|---|-----------|---------|--------|
| T1 | `PttMoveTargetOne_NoTarget_ReturnsNoOp` | YES (Skip=NT8-runtime) | YES |
| T2 | `PttMoveTargetOne_NoPosition_ReturnsNoOp` | YES (Skip=NT8-runtime) | YES |
| T3 | `PttMoveTargetOne_PartialFill_QtyIsQuantityMinusFilled` | YES (runs) | YES |
| T4 | `PttMoveTargetOne_ZeroFill_QtyIsQuantity` | YES (runs) | YES |
| T5 | `TryGetExitPrice_AskPlusZeroBuffer_ReturnsAskExactly` | YES (Skip=NT8-runtime) | YES |
| T6 | `TryGetExitPrice_AskPlusTwoBuffer_ReturnsAskPlusTwoTicks` | YES (Skip=NT8-runtime) | YES |
| T7 | `TryGetExitPrice_BidMinusOneBuffer_ReturnsBidMinusOneTick` | YES (Skip=NT8-runtime) | YES |
| T8 | `IsBeSwapGtcSweepGuard_ReturnsFalse_ForPttMaAskOrder` | YES (runs) | YES |

T3, T4, T8 are runnable without NT8 host (pure arithmetic / pure string method). T1/T2/T5/T6/T7 are skipped with `NT8-runtime` reason following the established pattern in CopyEngineTests.cs.

No [Theory] substitution. No NUnit. No MSTest.

---

## TryGetExitPrice Internal Confirmed

`TryGetExitPrice` is declared `internal static` (not `private static`) at line 92 of PttMoveTargetOne.cs.  
Accessible from `PropTraderTools.Tests` namespace via `InternalsVisibleTo("PropTraderTools.Tests")` at CopyEngine.cs L42.

---

## NT8 API Contract Compliance

| API | Used? | Compliance |
|-----|-------|-----------|
| `acc.Cancel(new Order[] { target })` | YES | AddOnBase-available. PASS |
| `acc.CreateOrder(13 args, name="PTT-MA-*", (CustomOrder)null)` | YES | 13-arg, PTT-prefixed, (CustomOrder)null last arg. PASS |
| `acc.Submit(new[] { newOrder })` | YES | AddOnBase-available. PASS |
| `AtmStrategyChangeStopTarget()` | NO | BANNED API absent. PASS |
| `AtmStrategyCreate()` | NO | BANNED API absent. PASS |
| `Account.Change()` | NO | BANNED API absent. PASS |
| `DateTime.Now` | NO | Not referenced. PASS |
| `NinjaTrader.Core.Globals.MaxDate` | YES | Correct expiry. PASS |

---

## Deviations from Ticket Spec

None. Implementation follows the ticket specification exactly.

---

## BUILD_PASS
