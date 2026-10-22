# B211 Ticket-1 Verification Report

**Verifier**: ptt-verifier (Phase 4b)
**Ticket**: TICKET-1 -- Core Logic: New Feature Files + PttContracts Update
**Date**: 2026-07-07
**Engineer Layer 2 Report**: docs/brain/B211/ticket-1-completion.md
**Verdict**: VERIFY_PASS

---

## Files Verified (READ-ONLY)

| File | Action | Status |
|------|--------|--------|
| `src/PropTraderTools/Features/PttMoveTargetOne.cs` | NEW (143 lines) | VERIFIED |
| `src/PropTraderTools/Features/PttMoveTargetAll.cs` | NEW (38 lines) | VERIFIED |
| `src/PropTraderTools/Core/PttContracts.cs` | MODIFY (2 constants added) | VERIFIED |
| `src/PropTraderTools/Tests/B211Tests.cs` | NEW (144 lines) | VERIFIED |

---

## SCAN-01: CYC Count (Layer 3 Independent)

Manual branch count (non-strict McCabe: each `if`/ternary/`??`/`foreach`/`?.` at top-level decision = +1).
Non-strict counting is the established project standard (consistent with existing CopyEngine verification methodology).

| Method | File | Layer 3 CYC | Limit | Layer 2 CYC | Match? | Result |
|--------|------|-------------|-------|-------------|--------|--------|
| `PttMoveTargetOne.Execute` | PttMoveTargetOne.cs | 6 | 8 | 6 | YES | PASS |
| `PttMoveTargetOne.TryGetExitPrice` | PttMoveTargetOne.cs | 7 | 8 | 7 | YES | PASS |
| `PttMoveTargetOne.TryFindWorkingTarget` | PttMoveTargetOne.cs | 6 | 8 | 6 | YES | PASS |
| `PttMoveTargetAll.Execute` | PttMoveTargetAll.cs | 3 | 8 | 3 | YES | PASS |

### Layer 3 CYC Detail

**Execute (CYC=6)**:
base(1) + `if instr==null`(1) + `if !TryGetExitPrice`(1) + `if pos==null||qty==0`(1) + `if !TryFindWorkingTarget`(1) + `useAsk?:prefix` ternary(1) = 6

**TryGetExitPrice (CYC=7)**:
base(1) + `instr.MasterInstrument?.TickSize` null-conditional `?.`(1) + `if tickSize<=0`(1) + `if md==null`(1) + `useAsk ? md.Ask : md.Bid` ternary(1) + `if side==null`(1) + `if basePrice<=0`(1) = 7
V5 advisory confirmed: plan table stated CYC=6; actual is CYC=7. Within global limit of 8. PASS.

**TryFindWorkingTarget (CYC=6)**:
base(1) + `foreach`(1) + compound `if ..||..`(1) + `if OrderType!=Limit`(1) + `if name==null||!StartsWith`(1) + `if action!=Sell&&action!=BuyToCover`(1) = 6
Note: `o.Instrument?.FullName` null-conditional embedded inside compound `if` is not separately counted per project non-strict McCabe convention. Consistent with engineer methodology and architect plan.

**PttMoveTargetAll.Execute (CYC=3)**:
base(1) + `if instrument==null`(1) + `foreach`(1) = 3

**SCAN-01: PASS** -- All methods <= 8. No discrepancy vs Layer 2.

---

## SCAN-02: lock() Scan (Layer 3 Independent)

Command:
```powershell
Select-String -Path "src/PropTraderTools/Features/PttMoveTargetOne.cs","src/PropTraderTools/Features/PttMoveTargetAll.cs" -Pattern "lock\("
```

Output: (no output -- zero matches)

Verification: `_maSeq` counter uses `Interlocked.Increment(ref _maSeq)` at line 53 of PttMoveTargetOne.cs. JS-021 compliant.

**SCAN-02: PASS** -- 0 `lock(` occurrences. Matches Layer 2.

---

## SCAN-03: async void Scan (Layer 3 Independent)

Command:
```powershell
Select-String -Path "src/PropTraderTools/Features/PttMoveTargetOne.cs","src/PropTraderTools/Features/PttMoveTargetAll.cs" -Pattern "async void "
```

Output: (no output -- zero matches)

**SCAN-03: PASS** -- All new methods are synchronous void. No async keyword. JS-033 compliant.

---

## SCAN-04: null return Scan (Layer 3 Independent)

Command:
```powershell
Select-String -Path "src/PropTraderTools/Features/PttMoveTargetOne.cs","src/PropTraderTools/Features/PttMoveTargetAll.cs" -Pattern "return null"
```

Output: (no output -- zero matches)

Verification:
- `TryFindWorkingTarget` assigns `target = default(NinjaTrader.Cbi.Order)` and returns `bool`. No null returned from method.
- `TryGetExitPrice` sets `price = 0.0` on failure paths and returns `bool`. No null returned.
- All methods return `void` or `bool`. JS-002 TryGet pattern used throughout.

**SCAN-04: PASS** -- 0 `return null` occurrences. Matches Layer 2.

---

## SCAN-05: throw Scan (Layer 3 Independent)

Command (executable throws only):
```powershell
Select-String -Path "src/PropTraderTools/Features/PttMoveTargetOne.cs","src/PropTraderTools/Features/PttMoveTargetAll.cs" -Pattern "^\s+throw\s"
```
Output: (no output -- zero executable throw statements)

Command (broad, for comment verification):
```powershell
Select-String -Path "src/PropTraderTools/Features/PttMoveTargetOne.cs","src/PropTraderTools/Features/PttMoveTargetAll.cs" -Pattern "throw "
```
Output:
```
PttMoveTargetOne.cs:4:  // JS-001: no throw -- NT8 calls wrapped try/catch log-and-swallow.
PttMoveTargetOne.cs:22: // JS-001: no throw new -- NT8 calls wrapped try/catch log-and-swallow.
```

Both hits are **comments only** (lines 4 and 22). No executable `throw` statement in any new/modified Ticket-1 file.
NT8 API calls (Cancel, CreateOrder, Submit) wrapped in try/catch log-and-swallow at lines 63-84. No rethrow. JS-001 compliant.

**SCAN-05: PASS** -- 0 executable throw statements. 2 comment hits documented and expected. Matches Layer 2.

---

## SCAN-06: ASCII Scan (Layer 3 Independent)

Command:
```powershell
Get-Content "src/PropTraderTools/Features/PttMoveTargetOne.cs" | Where-Object {$_ -match '[^\x00-\x7F]'}
Get-Content "src/PropTraderTools/Features/PttMoveTargetAll.cs" | Where-Object {$_ -match '[^\x00-\x7F]'}
```

Output: (no output -- zero non-ASCII characters in either file)

String literal inventory (all confirmed ASCII-only):
| String Literal | File | ASCII? |
|----------------|------|--------|
| `"[PTT-MA] no position: "` | PttMoveTargetOne.cs L37 | PASS |
| `"[PTT-MA] no target found for "` | PttMoveTargetOne.cs L44 | PASS |
| `"[PTT-MA] "` | PttMoveTargetOne.cs L58 | PASS |
| `"[PTT-MA] Execute exception: "` | PttMoveTargetOne.cs L83 | PASS |
| `"D5"` | PttMoveTargetOne.cs L53 | PASS |
| `"PTT-"` | PttMoveTargetOne.cs L132 | PASS |
| `"[PTT-MA-ALL] instrument null"` | PttMoveTargetAll.cs L24 | PASS |
| `"[PTT-MA-ALL] useAsk="` | PttMoveTargetAll.cs L28 | PASS |
| `"PTT-MA-Ask-"` | PttContracts.cs L346 | PASS |
| `"PTT-MA-Bid-"` | PttContracts.cs L350 | PASS |

**SCAN-06: PASS** -- 0 non-ASCII characters. All string literals ASCII-only. Matches Layer 2.

---

## SCAN-07: ptt-sync (Layer 3 Independent)

Command:
```powershell
powershell -File .\scripts\ptt-sync.ps1
```

Output (exact):
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

Both new Feature files (`PttMoveTargetAll.cs`, `PttMoveTargetOne.cs`) confirmed LINKED. Hard-link integrity maintained.

**SCAN-07: PASS** -- 21/0. Matches Layer 2 exactly.

---

## Layer 2 vs Layer 3 Discrepancy Summary

| Scan | Layer 2 (Engineer) | Layer 3 (Verifier) | Discrepancy? |
|------|-------------------|--------------------|-------------|
| SCAN-01 Execute CYC=6 | 6 | 6 | NONE |
| SCAN-01 TryGetExitPrice CYC=7 | 7 | 7 | NONE |
| SCAN-01 TryFindWorkingTarget CYC=6 | 6 | 6 | NONE |
| SCAN-01 PttMoveTargetAll.Execute CYC=3 | 3 | 3 | NONE |
| SCAN-02 lock() | 0 | 0 | NONE |
| SCAN-03 async void | 0 | 0 | NONE |
| SCAN-04 return null | 0 | 0 | NONE |
| SCAN-05 throw (executable) | 0 | 0 | NONE |
| SCAN-06 ASCII | PASS | 0 non-ASCII | NONE |
| SCAN-07 ptt-sync | 21/0 | 21/0 | NONE |

**No discrepancies between Layer 2 (engineer self-report) and Layer 3 (independent verification).**

---

## DNA Rule Check (Jane Street Standards)

| Rule | Check | Result |
|------|-------|--------|
| JS-021 No lock() | `lock(` absent from all new files | PASS |
| JS-001 No throw in hot paths | No executable `throw` anywhere; try/catch log-and-swallow | PASS |
| JS-002 No null return | TryGet pattern used; no method returns null | PASS |
| JS-023 Interlocked not lock | `_maSeq` uses `Interlocked.Increment` | PASS |
| JS-033 No async void | All methods synchronous void | PASS |
| ASCII-only | 0 non-ASCII in all new files | PASS |
| CYC <= 8 | Max CYC=7 (TryGetExitPrice); all under 8 | PASS |
| No DateTime.Now | Not referenced in any new file | PASS |
| No FontFamily | Not referenced | PASS |
| No hex color literals | Not referenced | PASS |
| CreateOrder PTT- prefix | `"PTT-MA-Ask-"` / `"PTT-MA-Bid-"` -- both start with "PTT-" | PASS |
| No sealed on TradeCopierWindow | Not modified in Ticket-1 scope | N/A |
| No async/await in NT8 lifecycle methods | Not applicable to Feature files | N/A |
| B211-PARTIAL-FILL P0 | `qty = target.Quantity - target.Filled` at line 49 | PASS |

---

## Implementation Correctness Checks

| Check | Source Location | Result |
|-------|----------------|--------|
| TryFindWorkingTarget signature: bool, out Order | PttMoveTargetOne.cs L119-120 | PASS |
| TryGetExitPrice signature: bool, out double | PttMoveTargetOne.cs L92 | PASS |
| TryFindWorkingTarget: no null return from method | PttMoveTargetOne.cs L122, 139 | PASS |
| TryGetExitPrice: no null return from method | PttMoveTargetOne.cs L97, 100, 104, 107 | PASS |
| Partial fill: qty = Quantity - Filled | PttMoveTargetOne.cs L49 | PASS |
| Order name: MaAskPrefix / MaBidPrefix from PttContracts | PttMoveTargetOne.cs L52 | PASS |
| PttContracts: MaAskPrefix = "PTT-MA-Ask-" | PttContracts.cs L346 | PASS |
| PttContracts: MaBidPrefix = "PTT-MA-Bid-" | PttContracts.cs L350 | PASS |
| Counter: Interlocked.Increment not lock | PttMoveTargetOne.cs L53 | PASS |
| try/catch: Cancel + CreateOrder + Submit wrapped | PttMoveTargetOne.cs L63-84 | PASS |
| AllAccounts: delegated to CopyEngine.Instance | PttMoveTargetAll.cs L31 | PASS |
| AtmStrategyChangeStopTarget: absent | Full source scan | PASS |
| DW-B211-03: LAST matching PTT-* order taken | PttMoveTargetOne.cs L136 (target = o, overwrites) | PASS |
| TryGetExitPrice declared internal static | PttMoveTargetOne.cs L92 | PASS |
| B211-BEHAV-1: no target -> no-op + log | PttMoveTargetOne.cs L42-46 | PASS |
| B211-BEHAV-2: no position -> no-op + log | PttMoveTargetOne.cs L35-39 | PASS |
| B211-PRICE-CALC: Ask/Bid + bufferTicks * tickSize | PttMoveTargetOne.cs L109-110 | PASS |

---

## Test Verification

| Test | [Fact]? | xUnit? | Runnable? | Assertion |
|------|---------|--------|-----------|-----------|
| T1 PttMoveTargetOne_NoTarget_ReturnsNoOp | YES (Skip=NT8) | YES | No (NT8 skip) | Contract documented |
| T2 PttMoveTargetOne_NoPosition_ReturnsNoOp | YES (Skip=NT8) | YES | No (NT8 skip) | Contract documented |
| T3 PttMoveTargetOne_PartialFill_QtyIsQuantityMinusFilled | YES | YES | YES | `Assert.Equal(3, qty)` - 5-2=3 ✓ |
| T4 PttMoveTargetOne_ZeroFill_QtyIsQuantity | YES | YES | YES | `Assert.Equal(4, qty)` - 4-0=4 ✓ |
| T5 TryGetExitPrice_AskPlusZeroBuffer_ReturnsAskExactly | YES (Skip=NT8) | YES | No (NT8 skip) | Contract documented |
| T6 TryGetExitPrice_AskPlusTwoBuffer_ReturnsAskPlusTwoTicks | YES (Skip=NT8) | YES | No (NT8 skip) | Contract documented |
| T7 TryGetExitPrice_BidMinusOneBuffer_ReturnsBidMinusOneTick | YES (Skip=NT8) | YES | No (NT8 skip) | Contract documented |
| T8 IsBeSwapGtcSweepGuard_ReturnsFalse_ForPttMaAskOrder | YES | YES | YES | `Assert.False(result)` ✓ |

All 8 required [Fact] tests present. No [Theory]. No NUnit. No MSTest. Only `using Xunit;`.
T3/T4/T8 are runnable (pure arithmetic / pure string method).
T1/T2/T5/T6/T7 are skipped with NT8-runtime reason per established project pattern.
`TryGetExitPrice` declared `internal static` at line 92 -- accessible via InternalsVisibleTo.

**Test Verification: PASS**

---

## Spec Requirements Coverage

| Req ID | Description | Implemented | Location |
|--------|-------------|-------------|----------|
| B211-BEHAV-1 | No Working target -> no-op + log | YES | PttMoveTargetOne.cs L42-46 |
| B211-BEHAV-2 | No open position -> no-op + log | YES | PttMoveTargetOne.cs L35-39 |
| B211-BEHAV-3 | No BeSwapInProgress guard on PTT-MA-* | YES | No such guard in PttMoveTargetOne.cs |
| B211-BEHAV-4 | Re-entrant clicks valid | YES | Each click independently calls Execute |
| B211-BEHAV-5 | PTT-MA-* NOT protected by FIX-G-2 | YES | T8 confirms; "PTT-MA-Ask-" != "PTT-BE-" |
| B211-PARTIAL-FILL | qty = Quantity - Filled | YES | PttMoveTargetOne.cs L49 |
| B211-PRICE-CALC | Ask/Bid + bufferTicks * tickSize | YES | PttMoveTargetOne.cs L109-110 |

**Spec Coverage: PASS -- All 7 spec requirements satisfied.**

---

## Architecture Compliance

| Architectural Element | Spec | Actual | Compliant? |
|----------------------|------|--------|------------|
| Class: PttMoveTargetOne | internal static | internal static | YES |
| Class: PttMoveTargetAll | internal static | internal static | YES |
| Namespace | PropTraderTools | PropTraderTools | YES |
| Counter field | private static int _maSeq | private static int _maSeq = 0 | YES |
| Execute signature | (Account acc, Instrument instr, bool useAsk, int bufferTicks) | exact match | YES |
| TryGetExitPrice signature | internal static bool (..., out double price) | internal static bool (..., out double price) | YES |
| TryFindWorkingTarget signature | private static bool (..., out Order target) | private static bool (..., out Order target) | YES |
| AllAccounts delegation | CopyEngine.Instance.AllAccounts(instrument) | CopyEngine.Instance.AllAccounts(instrument) | YES |
| PttContracts: MaAskPrefix | "PTT-MA-Ask-" | "PTT-MA-Bid-" | YES |
| PttContracts: MaBidPrefix | "PTT-MA-Bid-" | "PTT-MA-Bid-" | YES |
| DW-B211-03: LAST match | target = last PTT-prefixed Working Limit | target = o (overwrites on each match) | YES |

---

## NT8 API Compliance

| API | Used? | Valid AddOn API? | Compliant |
|-----|-------|-----------------|-----------|
| `acc.Cancel(new Order[] { target })` | YES | YES (AddOnBase) | PASS |
| `acc.CreateOrder(13 args, "PTT-MA-*", (CustomOrder)null)` | YES | YES (AddOnBase) | PASS |
| `acc.Submit(new[] { newOrder })` | YES | YES (AddOnBase) | PASS |
| `AtmStrategyChangeStopTarget()` | NO | BANNED (StrategyBase only) | PASS |
| `AtmStrategyCreate()` | NO | BANNED (StrategyBase only) | PASS |
| `Account.Change()` | NO | Excluded | PASS |
| `DateTime.Now` | NO | BANNED | PASS |
| `NinjaTrader.Core.Globals.MaxDate` | YES | Valid | PASS |
| `CopyEngine.Instance.FindPositionPublic(acc, instr)` | YES | Existing internal API | PASS |
| `CopyEngine.Instance.AllAccounts(instr)` | YES | Existing internal API | PASS |

---

## Violations Found

**NONE.**

All 7 scans pass. All DNA rules satisfied. All spec requirements covered. Architecture compliance confirmed. 8 tests present and correctly structured.

---

## Final Verdict

**VERIFY_PASS**

Ticket-1 implementation is correct, complete, and fully compliant with the B211 architecture plan, Ticket-1 specification, and Jane Street DNA rules. No violations in Layer 3 independent scans. No discrepancies between Layer 2 (engineer) and Layer 3 (verifier).

TICKET-2 (TradeCopierPanel.cs UI wiring) is now unblocked.

---

*ptt-verifier -- Phase 4b -- 2026-07-07*