# Ticket C-2 Completion Report

**Ticket**: C-2 -- CCN Reduction: PttBreakEvenSwap + PttTrim + PttFlatten + PttCancel
**Engineer**: ptt-engineer
**Date**: 2026-09-06
**Workspace**: `C:\WSGTA\ptt-lane-c\` (branch `bwave-refactor-lane-c`)
**Status**: BUILD_PASS

---

## [SCOPE LOCK CONFIRMED: Ticket C-2 only]

Files touched in this session (C-2 scope only):
- `src/PropTraderTools/Features/PttBreakEvenSwap.cs` -- MODIFIED
- `src/PropTraderTools/Features/PttTrim.cs` -- MODIFIED
- `src/PropTraderTools/Features/PttFlatten.cs` -- MODIFIED
- `src/PropTraderTools/Features/PttCancel.cs` -- MODIFIED
- `src/PropTraderTools/Tests/BwaveRefactorLaneCTests.cs` -- MODIFIED (C-2 tests appended)

C-1 files (PttQuickExit.cs, PttGlobalQuickExit.cs, PttBreakEven.cs) NOT touched.
CopyEngine.cs, TradeCopierPanel.cs, TradeCopierWindow.cs NOT touched.

---

## Summary of Changes

### PttBreakEvenSwap.cs

**New helpers added**:

1. `private static void SubmitBareStopSwap(Account acc, Instrument instr, bool isLong, OrderAction stopDir, double newStop, int posQty)`
   - Extracted from `Execute` 0-targets block (original lines 78-119)
   - Contains: `IsStopPriceSubmittable` check, try/catch for CreateOrder+Submit, else-log path
   - NT8-049: arg6=0, arg7=newStop. NT8-007: (CustomOrder)null. NT8-013: DateTime.MaxValue. NT8-014: "PTT-BE-Stop"
   - CYC=4: (1) if(IsStopPriceSubmittable), (2) try/catch, (3) bareStop null check, (4) else-log

2. `private static void SubmitSwapPair(Account acc, Instrument instr, bool isLong, OrderAction stopDir, double newStop, string ocoId_i, int i, (double Price, int Qty, OrderAction Action) t)`
   - Extracted from `Execute` for-loop body (original lines 134-204)
   - Contains: stop IsStopPriceSubmittable check, stop try/catch, else-log, target try/catch
   - NT8-049: stop arg6=0 arg7=newStop; target arg6=t.Price arg7=0. NT8-014: "PTT-BE-Stop-N", "PTT-BE-Target-N"
   - CYC=4: (1) if(IsStopPriceSubmittable), (2) stop try/catch, (3) else-log, (4) target try/catch

**`Execute` after extraction**: CCN=8 (base 1 + acc||instr=1 + pos||qty=1 + isLong ternary=1 + targets||Count=1 + targets.Count branch=1 + for-loop=1 = 7+base = 8). No behavior change.

---

### PttTrim.cs

**New helper added**:

1. `private static (OrderType orderType, double limitPrice, double stopPrice) ResolveOrderParams(Position pos, int buffer, double ask, double bid, double tickSize)`
   - Extracted from `TrimPositionLocal` useLimitOrder block (original lines 113-136)
   - Returns value tuple (never null -- JS-002 satisfied)
   - CYC=5: (1) tickSize>0, (2) &&Long?ask:bid, (3) ternary in useLimitOrder, (4) if(useLimitOrder), (5) limitPrice ternary

**`TrimPositionLocal` after extraction**: CCN=6 (acc||instr||qty guard=3 || ops, direction ternary=1, try/catch=1, order null check=1). No behavior change. Log line updated: `useLimitOrder ? ...` replaced with `orderType == OrderType.Limit ? ...` (equivalent).

---

### PttFlatten.cs

**New helper added**:

1. `private static (OrderType orderType, double limitPrice, double stopPrice) ResolveOrderParams(Position pos, int buffer, double ask, double bid, double tickSize)`
   - Structurally identical to PttTrim.ResolveOrderParams
   - Returns value tuple (never null -- JS-002 satisfied)
   - CYC=5: same as PttTrim.ResolveOrderParams

**`FlattenPositionLocal` after extraction**: CCN=6 (acc||instr||pos guard=3 || ops, direction ternary=1, try/catch=1, order null check=1). No behavior change. Log line updated: `useLimitOrder ? ...` replaced with `orderType == OrderType.Limit ? ...` (equivalent).

---

### PttCancel.cs

**New helper added**:

1. `private static bool IsWorkingEntryOrder(Order o, Instrument instr)`
   - Extracted from `CancelWorkingEntriesLocal` compound filter (stateOk && instrOk inline block)
   - Returns bool (JS-002 satisfied)
   - CYC=4: (1) o null check, (2) stateOk Working||Initialized, (3) instrOk o.Instrument!=null, (4) FullName comparison

**`CancelWorkingEntriesLocal` after extraction**: CCN=5 (acc||instr=1, foreach=1, IsWorkingEntryOrder=1, count==0=1, try/catch=1 + base=1 = 6). No behavior change.

---

### BwaveRefactorLaneCTests.cs

5 new [Fact] tests appended (C-2 section):

```
PttBreakEvenSwap_SubmitBareStopSwap_Exists  -- asserts 6 params
PttBreakEvenSwap_SubmitSwapPair_Exists       -- asserts 8 params
PttTrim_ResolveOrderParams_Exists            -- asserts 5 params
PttFlatten_ResolveOrderParams_Exists         -- asserts 5 params
PttCancel_IsWorkingEntryOrder_Exists         -- asserts 2 params
```

All xUnit [Fact], reflection-based only, no NUnit, no MSTest.

---

## Build Output

```
Build succeeded.
C:\WSGTA\ptt-lane-c\src\PropTraderTools\Tests\B131Tests.cs(165,13): warning xUnit2004: ...
    1 Warning(s)   [pre-existing, not in C-2 scope]
    0 Error(s)
```

**Result**: 0 errors. BUILD_PASS.

---

## Scan Results

### SCAN-01: lock() grep
```
Get-ChildItem C:\WSGTA\ptt-lane-c\src\PropTraderTools\ -Filter "*.cs" -Recurse | Select-String -Pattern "lock\("
```
Result: All hits are in comment text ("no lock()") -- zero actual lock() statements. **0 violations. PASS.**

### SCAN-02: Non-ASCII characters
```
Get-Content [C-2 changed files] | Where-Object { $_ -match '[^\x00-\x7F]' }
```
Result: No output. **0 non-ASCII chars. PASS.**

### SCAN-03: FontFamily
```
Get-ChildItem ... -Recurse | Select-String -Pattern "FontFamily"
```
Result: All hits in comment text ("No FontFamily") -- zero actual assignments. **0 violations. PASS.**

### SCAN-04: Hex color literals
```
Get-ChildItem ... -Recurse | Select-String -Pattern "#[0-9A-Fa-f]{6}"
```
Result: All hits in TradeCopierPanel.cs/TradeCopierWindow.cs comment text (e.g., `// green #22c55e`) -- no hex in code strings. No hits in C-2 files. **0 violations. PASS.**

### SCAN-05: PTT- prefix on CreateOrder calls
New CreateOrder signal names in C-2 helpers:
- `"PTT-BE-Stop"` in SubmitBareStopSwap -- starts with PTT-. PASS.
- `"PTT-BE-Stop-" + (i + 1)` in SubmitSwapPair -- starts with PTT-. PASS.
- `"PTT-BE-Target-" + (i + 1)` in SubmitSwapPair -- starts with PTT-. PASS.
- `"PTT-Trim"` in PttTrim (unchanged) -- starts with PTT-. PASS.
- `"PTT-Flatten"` in PttFlatten (unchanged) -- starts with PTT-. PASS.

**0 violations. PASS.**

### SCAN-06: DateTime.Now
```
Get-ChildItem ... -Recurse | Select-String -Pattern "DateTime\.Now[^U]"
```
Result: All hits in comment text ("No DateTime.Now") -- zero actual usage. **0 violations. PASS.**

### SCAN-07: Lizard CCN
```powershell
$files = Get-ChildItem C:\WSGTA\ptt-lane-c\src\PropTraderTools\ -Filter "*.cs" -Recurse |
  Where-Object { $_.FullName -notmatch '\\obj\\' -and $_.FullName -notmatch '\\bin\\' }
lizard $files --csv 2>&1 | ConvertFrom-Csv ... | Where-Object { [int]$_.CCN -gt 8 } | Sort-Object ...
```
Result: No output. **0 rows with CCN > 8. PASS.**

---

## NT8 Sync Result

```
=== PTT SYNC: src/PropTraderTools -> NT8 AddOns ===
  COPIED:  Features\PttBreakEvenSwap.cs
  COPIED:  Features\PttCancel.cs
  COPIED:  Features\PttFlatten.cs
  COPIED:  Features\PttTrim.cs

  Copied:   4  |  In-sync: 14  |  Excluded: 65

=== PTT VERIFY: MD5 check every synced file ===
  OK       AtrSizingEngine.cs
  OK       CopyEngine.cs
  OK       FeatureFlags.cs
  OK       LicenseClient.cs
  OK       TradeCopierAddOn.cs
  OK       TradeCopierPanel.cs
  OK       TradeCopierWindow.cs
  OK       Core\PttContracts.cs
  OK       Features\PttBreakEven.cs
  OK       Features\PttBreakEvenSwap.cs
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

**Result: 18/18 OK, 0 MISMATCH. PASS.**

---

## CCN Summary (C-2 methods)

| Method | File | CCN Before | CCN After | PASS? |
|--------|------|-----------|-----------|-------|
| `Execute` | PttBreakEvenSwap | 15 | 8 | YES |
| `SubmitBareStopSwap` | PttBreakEvenSwap | NEW | 4 | YES |
| `SubmitSwapPair` | PttBreakEvenSwap | NEW | 4 | YES |
| `TrimPositionLocal` | PttTrim | 13 | 6 | YES |
| `ResolveOrderParams` | PttTrim | NEW | 5 | YES |
| `FlattenPositionLocal` | PttFlatten | 13 | 6 | YES |
| `ResolveOrderParams` | PttFlatten | NEW | 5 | YES |
| `CancelWorkingEntriesLocal` | PttCancel | 10 | 6 | YES |
| `IsWorkingEntryOrder` | PttCancel | NEW | 4 | YES |

All methods: CCN <= 8. All 7 scans: PASS.

---

## JS Rules Compliance

| Rule | Status |
|------|--------|
| JS-002 (no return null) | All new helpers: void or value tuple or bool -- no null return |
| JS-021 (no lock) | No lock() in any new helper |
| JS-001 (no throw) | SubmitBareStopSwap, SubmitSwapPair: try/catch, no throw |
| JS-033 (no async void) | All helpers synchronous |
| ASCII-only | All new identifiers ASCII-only confirmed by SCAN-02 |

---

## F5 Reminder

**MANDATORY NEXT STEP**: Press F5 in NinjaTrader 8 to compile the AddOn after NT8 sync.
