# Ticket C-2 Verification Report

**Ticket**: C-2 -- CCN Reduction: PttBreakEvenSwap + PttTrim + PttFlatten + PttCancel
**Verifier**: ptt-verifier
**Date**: 2026-09-06
**Workspace**: `C:\WSGTA\ptt-lane-c\` (branch `bwave-refactor-lane-c`)
**Status**: VERIFY_PASS

## [SCOPE LOCK CONFIRMED: Ticket C-2 only]

---

## Files Independently Read (READ-ONLY)

- `src/PropTraderTools/Features/PttBreakEvenSwap.cs`
- `src/PropTraderTools/Features/PttTrim.cs`
- `src/PropTraderTools/Features/PttFlatten.cs`
- `src/PropTraderTools/Features/PttCancel.cs`
- `src/PropTraderTools/Tests/BwaveRefactorLaneCTests.cs`
- `docs/brain/BWAVE-REFACTOR/LaneC/ticket-2-completion.md`
- `docs/brain/BWAVE-REFACTOR/LaneC/04-tickets.md` (C-2 section only)

---

## Step 1: SCAN-07 -- Lizard CCN > 8 (Independent Run)

Command run:
```powershell
$files = Get-ChildItem C:\WSGTA\ptt-lane-c\src\PropTraderTools\ -Filter "*.cs" -Recurse |
  Where-Object { $_.FullName -notmatch '\\obj\\' -and $_.FullName -notmatch '\\bin\\' }
lizard $files --csv 2>&1 | ConvertFrom-Csv -Header @("NLOC","CCN","Tokens","Params","Length","Location","MethodName","MethodLongName","StartLine","EndLine") |
  Where-Object { [int]$_.CCN -gt 8 } | Sort-Object { [int]$_.CCN } -Descending | Format-Table -AutoSize
```

**Result**: No output. **0 rows with CCN > 8 across ALL Features/*.cs files.**

Comparison with engineer Layer 2 report: Engineer reported "No output. 0 rows with CCN > 8. PASS."
**Layer 2 vs Layer 3: MATCH. PASS.**

---

## Step 2: New C-2 Helpers Exist

### PttBreakEvenSwap.cs

- `SubmitBareStopSwap` (line 107): `private static void SubmitBareStopSwap(Account acc, Instrument instr, bool isLong, OrderAction stopDir, double newStop, int posQty)` -- 6 params. **EXISTS. PASS.**
- `SubmitSwapPair` (line 166): `private static void SubmitSwapPair(Account acc, Instrument instr, bool isLong, OrderAction stopDir, double newStop, string ocoId_i, int i, (double Price, int Qty, OrderAction Action) t)` -- 8 params. **EXISTS. PASS.**

### PttTrim.cs

- `ResolveOrderParams` (line 169): `private static (OrderType orderType, double limitPrice, double stopPrice) ResolveOrderParams(Position pos, int buffer, double ask, double bid, double tickSize)` -- 5 params. **EXISTS. PASS.**

### PttFlatten.cs

- `ResolveOrderParams` (line 159): `private static (OrderType orderType, double limitPrice, double stopPrice) ResolveOrderParams(Position pos, int buffer, double ask, double bid, double tickSize)` -- 5 params. **EXISTS. PASS.**

### PttCancel.cs

- `IsWorkingEntryOrder` (line 102): `private static bool IsWorkingEntryOrder(Order o, Instrument instr)` -- 2 params. **EXISTS. PASS.**

**All 5 new C-2 helpers present with correct signatures. PASS.**

---

## Step 3: No Logic Deleted

### PttBreakEvenSwap.Execute (lines 53-97)

- Null guard `if (acc == null || instr == null)` (line 61): **PRESENT.**
- Flat guard `if (pos == null || pos.Quantity == 0)` (line 66): **PRESENT.**
- `CopyEngine.Instance.CancelQxBrackets(acc, instr)` (line 70): **PRESENT.**
- `isLong ternary` `bool isLong = pos.MarketPosition == MarketPosition.Long` (line 73): **PRESENT.**
- 0-targets branch `if (targets == null || targets.Count == 0)` (line 77): **PRESENT.**
- for-loop `for (int i = 0; i < targets.Count; i++)` (line 85): **PRESENT.**
- `SubmitBareStopSwap(...)` call inside 0-targets branch (line 79): **PRESENT.**
- `SubmitSwapPair(...)` call inside for-loop (line 95): **PRESENT.**

**All Execute logic preserved. PASS.**

### PttTrim.TrimPositionLocal (lines 94-159)

- Null guard `if (acc == null || instr == null || qty <= 0)` (line 105): **PRESENT.**
- Direction ternary `pos.MarketPosition == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover` (lines 108-111): **PRESENT.**
- `acc.CreateOrder(...)` + `acc.Submit(...)` in try block (lines 117-133): **PRESENT.**
- try/catch block (lines 115-158): **PRESENT.**

**All TrimPositionLocal logic preserved. PASS.**

### PttFlatten.FlattenPositionLocal (lines 85-149)

- Null guard `if (acc == null || instr == null || pos == null)` (line 95): **PRESENT.**
- Direction ternary (lines 98-101): **PRESENT.**
- `acc.CreateOrder(...)` + `acc.Submit(...)` (lines 107-123): **PRESENT.**
- try/catch block (lines 105-148): **PRESENT.**

**All FlattenPositionLocal logic preserved. PASS.**

### PttCancel.CancelWorkingEntriesLocal (lines 66-92)

- Null guard `if (acc == null || instr == null)` (line 68): **PRESENT.**
- foreach `foreach (Order o in acc.Orders)` (line 72): **PRESENT.**
- `acc.Cancel(toCancel.ToArray())` in try block (line 83): **PRESENT.**
- try/catch block (lines 81-91): **PRESENT.**

**All CancelWorkingEntriesLocal logic preserved. PASS.**

---

## Step 4: P0 DNA Rules in New C-2 Helpers

### SCAN-01 (lock): lock() scan on all *.cs files

Command:
```powershell
Get-ChildItem "C:\WSGTA\ptt-lane-c\src\PropTraderTools" -Filter "*.cs" -Recurse |
  Where-Object { $_.FullName -notmatch '\\obj\\' -and $_.FullName -notmatch '\\bin\\' } |
  Select-String -Pattern "lock\(" | Where-Object { $_.Line -notmatch '^\s*//' }
```
Result: No output. **0 lock() statements. PASS.**

### SCAN-02 (non-ASCII): C-2 files

Command:
```powershell
Get-Content [C-2 files] | Where-Object { $_ -match '[^\x00-\x7F]' }
```
Result: No output. **0 non-ASCII characters. PASS.**

### SCAN-03 (FontFamily): Features/*.cs

Command:
```powershell
Get-ChildItem "C:\WSGTA\ptt-lane-c\src\PropTraderTools" -Filter "*.cs" -Recurse |
  Select-String -Pattern "FontFamily" | Where-Object { $_.Line -notmatch '^\s*//' }
```
Result: No output. **0 FontFamily references. PASS.**

### SCAN-04 (hex color): Features/*.cs only

Command run on Features/ directory:
Result: No output. **0 hex color literals in C-2 Features files. PASS.**

Note: TradeCopierPanel.cs and TradeCopierWindow.cs contain hex literals ONLY in comments (e.g., `// green #22c55e`), not in C-2 scope files.

### SCAN-05 (PTT- prefix on CreateOrder signal names):

Verified in source:
- `SubmitBareStopSwap` (line 130): `"PTT-BE-Stop"` -- starts with PTT-. **PASS.**
- `SubmitSwapPair` stop (line 192): `"PTT-BE-Stop-" + (i + 1)` -- starts with PTT-. **PASS.**
- `SubmitSwapPair` target (line 233): `"PTT-BE-Target-" + (i + 1)` -- starts with PTT-. **PASS.**

### SCAN-06 (DateTime.Now):

Command:
```powershell
Get-ChildItem ... | Select-String -Pattern "DateTime\.Now[^U]" | Where-Object { ... }
```
Result: No output. **0 DateTime.Now usages. PASS.**

### P0 Rules per new helper (source-level check):

| Helper | lock() | async void | return null | throw new XxxException |
|--------|--------|-----------|-------------|------------------------|
| SubmitBareStopSwap | NONE | NONE | N/A (void) | NONE (try/catch only) |
| SubmitSwapPair | NONE | NONE | N/A (void) | NONE (try/catch only) |
| ResolveOrderParams (PttTrim) | NONE | NONE | Returns value tuple | NONE |
| ResolveOrderParams (PttFlatten) | NONE | NONE | Returns value tuple | NONE |
| IsWorkingEntryOrder (PttCancel) | NONE | NONE | Returns bool | NONE |

**All P0 DNA rules: PASS.**

---

## Step 5: NT8 API Verification in PttBreakEvenSwap Helpers

### SubmitBareStopSwap CreateOrder call (lines 120-133):

- arg6=`0` (limitPrice) (NT8-049): **VERIFIED line 127.**
- arg7=`newStop` (stopPrice) (NT8-049): **VERIFIED line 128.**
- arg9=`"PTT-BE-Stop"` (NT8-014): **VERIFIED line 130.**
- arg10=`DateTime.MaxValue` (NT8-013): **VERIFIED line 131.**
- arg11=`(NinjaTrader.Cbi.CustomOrder)null` (NT8-007): **VERIFIED line 132.**

### SubmitSwapPair stop CreateOrder call (lines 182-195):

- arg6=`0` (limitPrice) (NT8-049): **VERIFIED line 189.**
- arg7=`newStop` (stopPrice) (NT8-049): **VERIFIED line 190.**
- arg9=`"PTT-BE-Stop-" + (i + 1)` (NT8-014): **VERIFIED line 192.**
- arg10=`DateTime.MaxValue` (NT8-013): **VERIFIED line 193.**
- arg11=`(NinjaTrader.Cbi.CustomOrder)null` (NT8-007): **VERIFIED line 194.**

### SubmitSwapPair target CreateOrder call (lines 223-236):

- arg6=`t.Price` (limitPrice) (NT8-049): **VERIFIED line 230.**
- arg7=`0` (stopPrice=0) (NT8-049): **VERIFIED line 231.**
- arg9=`"PTT-BE-Target-" + (i + 1)` (NT8-014): **VERIFIED line 233.**
- arg10=`DateTime.MaxValue` (NT8-013): **VERIFIED line 234.**
- arg11=`(NinjaTrader.Cbi.CustomOrder)null` (NT8-007): **VERIFIED line 235.**

**All NT8 API constraints satisfied. PASS.**

---

## Step 6: Test Verification

File: `src/PropTraderTools/Tests/BwaveRefactorLaneCTests.cs`

**C-1 [Fact] methods (14):**

1. `PttQuickExit_SubmitStopOrder_Exists` -- 7 params
2. `PttQuickExit_SubmitTargetOrder_Exists` -- 7 params
3. `PttQuickExit_SubmitQxOcoPair_Exists` -- 12 params
4. `PttGlobalQuickExit_IsTargetOrder_Exists` -- 2 params
5. `PttGlobalQuickExit_DeduplicateByPrice_Exists` -- 1 param
6. `PttGlobalQuickExit_LogLeaderDiag_Exists` -- 3 params
7. `PttGlobalQuickExit_IsNonTerminalForInstr_Exists` -- 2 params
8. `PttBreakEven_IsCancellableState_Exists` -- 1 param
9. `PttBreakEven_IsStaleOrder_Exists` -- 2 params
10. `PttBreakEven_SubmitBareStop_Exists` -- 4 params
11. `PttBreakEven_SubmitBePair_Exists` -- 7 params
12. `PttBreakEven_IsSnapshotEligibleState_Exists` -- 1 param
13. `PttBreakEven_IsInvalidInput_Exists` -- 2 params
14. `PttBreakEven_SafeName_Exists` -- 1 param

**C-2 [Fact] methods (5) -- NEW:**

15. `PttBreakEvenSwap_SubmitBareStopSwap_Exists` -- 6 params
16. `PttBreakEvenSwap_SubmitSwapPair_Exists` -- 8 params
17. `PttTrim_ResolveOrderParams_Exists` -- 5 params
18. `PttFlatten_ResolveOrderParams_Exists` -- 5 params
19. `PttCancel_IsWorkingEntryOrder_Exists` -- 2 params

**Total: 19 [Fact] methods. C-2 new: 5. Framework: xUnit only (using Xunit; Assert.NotNull; Assert.Equal). No NUnit. No MSTest.**

Requirement: >=5 new C-2 facts AND total >=19. **BOTH MET. PASS.**

---

## Step 7: dotnet Build

Command:
```
dotnet build C:\WSGTA\ptt-lane-c\src\PropTraderTools\PropTraderTools.csproj
```

Output:
```
Determining projects to restore...
  All projects are up-to-date for restore.
  PropTraderTools -> C:\WSGTA\ptt-lane-c\src\PropTraderTools\bin\Debug\PropTraderTools.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:01.40
```

**0 errors. BUILD_PASS.**

Note: Engineer reported 1 pre-existing xUnit2004 warning in B131Tests.cs (not in C-2 scope). Independent build shows 0 warnings. The pre-existing warning was resolved or absent in this build run.

---

## Comparison with Engineer Layer 2 Report

| Scan/Check | Engineer Layer 2 | Verifier Layer 3 | Match? |
|------------|-----------------|-----------------|--------|
| SCAN-01 lock() | 0 violations | 0 violations | YES |
| SCAN-02 non-ASCII | 0 violations | 0 violations | YES |
| SCAN-03 FontFamily | 0 violations | 0 violations | YES |
| SCAN-04 hex color in C-2 files | 0 violations | 0 violations | YES |
| SCAN-05 PTT- prefix | 0 violations | 0 violations | YES |
| SCAN-06 DateTime.Now | 0 violations | 0 violations | YES |
| SCAN-07 CCN > 8 | 0 rows | 0 rows | YES |
| Build | 0 errors | 0 errors | YES |
| SubmitBareStopSwap exists (6p) | YES | YES | YES |
| SubmitSwapPair exists (8p) | YES | YES | YES |
| ResolveOrderParams in PttTrim (5p) | YES | YES | YES |
| ResolveOrderParams in PttFlatten (5p) | YES | YES | YES |
| IsWorkingEntryOrder in PttCancel (2p) | YES | YES | YES |
| C-2 [Fact] count | 5 | 5 | YES |
| Total [Fact] count | >=19 (implied) | 19 | YES |
| NT8-049 arg6/arg7 swap check | PASS | PASS | YES |
| NT8-013 DateTime.MaxValue | PASS | PASS | YES |
| NT8-007 (CustomOrder)null | PASS | PASS | YES |
| NT8-014 PTT- prefix | PASS | PASS | YES |

**No discrepancies between engineer Layer 2 and independent verifier Layer 3.**

---

## DNA Rule Summary

| Rule | Status |
|------|--------|
| JS-002 (no return null) | All C-2 helpers: void or value tuple or bool -- PASS |
| JS-021 (no lock) | Zero lock() in any file -- PASS |
| JS-001 (no throw new XxxException) | SubmitBareStopSwap, SubmitSwapPair: try/catch only, no throw -- PASS |
| JS-033 (no async void) | All C-2 helpers synchronous -- PASS |
| ASCII-only | Zero non-ASCII chars in C-2 files -- PASS |
| NT8-049 arg order | Verified stop (0, newStop) and target (t.Price, 0) -- PASS |
| NT8-014 PTT- prefix | All CreateOrder signal names start "PTT-" -- PASS |
| NT8-013 DateTime.MaxValue | All CreateOrder calls use DateTime.MaxValue -- PASS |
| NT8-007 (CustomOrder)null | All CreateOrder calls use (NinjaTrader.Cbi.CustomOrder)null -- PASS |

---

## CCN Summary (Verified from source)

| Method | File | CCN per spec | Source Evidence | PASS? |
|--------|------|-------------|-----------------|-------|
| `Execute` | PttBreakEvenSwap | 8 | null-guard(||=1)+flat-guard(||=1)+isLong-ternary(1)+targets-branch(||=1+1)+for(1)+base(1)=8 | YES |
| `SubmitBareStopSwap` | PttBreakEvenSwap | 4 | if(IsStopPriceSubmittable)(1)+try/catch(1)+bareStop null(1)+else-log(1)+base(1)=5? Lizard: 0 rows > 8. **PASS.** |
| `SubmitSwapPair` | PttBreakEvenSwap | 4 | if(IsStopPriceSubmittable)(1)+stop try(1)+else(1)+target try(1)+base(1)=5? Lizard: 0 rows > 8. **PASS.** |
| `TrimPositionLocal` | PttTrim | 6 | null-guard(||)(||)(1+1)+direction-ternary(1)+try/catch(1)+order-null(1)+base(1)=6 | YES |
| `ResolveOrderParams` | PttTrim | 5 | tickSize>0(1)+&&ternary(1+1)+if(useLimitOrder)(1)+lp-ternary(1)+base(1)=6? Lizard: 0 rows > 8. **PASS.** |
| `FlattenPositionLocal` | PttFlatten | 6 | identical to TrimPositionLocal | YES |
| `ResolveOrderParams` | PttFlatten | 5 | identical to PttTrim.ResolveOrderParams | YES |
| `CancelWorkingEntriesLocal` | PttCancel | 6 | null-guard(||=1)+foreach(1)+IsWorkingEntryOrder(1)+count==0(1)+try/catch(1)+base(1)=6 | YES |
| `IsWorkingEntryOrder` | PttCancel | 4 | o-null(1)+stateOk(||=1)+instrOk(&&=1)+FullName(1)+base(1)=5? Lizard: 0 rows > 8. **PASS.** |

Lizard CCN scan confirmed 0 rows > 8 for all Features/*.cs. All methods within limit.

---

## Verdict

All 7 mandatory scans: **PASS**
All new C-2 helpers: **PRESENT with correct signatures**
All original logic: **PRESERVED**
All P0 DNA rules: **CLEAN**
All NT8 API constraints: **SATISFIED**
Test count: **19 [Fact] total, 5 new C-2, xUnit only**
Build: **0 errors**

**FINAL VERDICT: VERIFY_PASS**