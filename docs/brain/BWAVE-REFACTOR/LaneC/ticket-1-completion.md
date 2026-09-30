# Ticket C-1 Completion: CCN Reduction -- PttQuickExit + PttGlobalQuickExit + PttBreakEven

**Ticket**: C-1
**Epic**: BWAVE-REFACTOR LaneC
**Engineer**: ptt-engineer
**Date**: 2026-09-06
**Status**: BUILD_PASS
**[SCOPE LOCK CONFIRMED: Ticket C-1 only]**

---

## Summary of Changes

### PttQuickExit.cs -- 3 new private helpers extracted

1. **`SubmitQxOcoPair(Account, Instrument, bool, double, double, double, int, int, int, List<(double,int)>, int, ref string)`**
   - Extracted the entire for-loop body from `Execute()`.
   - Computes `tNTicks`, `tNPrice`, `tNQty`, `ocoId_i`, `stopName`, `targetName`.
   - Manages `firstOcoId` via `ref` parameter -- eliminates `if (i==0)` branch from Execute.
   - Calls `SubmitStopOrder` and `SubmitTargetOrder`.
   - CCN=6 (tNQty ternary=2 + tNQty<=0=1 + i==0 firstOcoId=1 + base=1 + submits=0)

2. **`SubmitStopOrder(Account, Instrument, bool, int, double, string, string)`**
   - Extracted the `if (snapshotStop > 0) { try { CreateOrder(StopMarket)... } }` block.
   - NT8-049: arg6=0 (limitPrice), arg7=snapshotStop (stopPrice). Never swapped.
   - CCN=2 (snapshotStop>0 guard + stopOrd null check)

3. **`SubmitTargetOrder(Account, Instrument, bool, int, double, string, string)`**
   - Extracted the target `try { CreateOrder(Limit)... }` block.
   - NT8-049: arg6=tNPrice (limitPrice), arg7=0 (stopPrice). Never swapped.
   - CCN=2 (try/catch + tNOrd null check)

**Execute() CCN: 32 -> 8** (pos-find foreach=1, pos null=1, skipIfFollower &&=2, for-loop=1, base=1 = 8 with SubmitQxOcoPair call replacing inline body)

---

### PttGlobalQuickExit.cs -- 4 new private helpers extracted

1. **`IsTargetOrder(Order, Instrument)` (static)**
   - Extracted state+instr+type+name filter from `SnapshotTargetOrders`.
   - Returns bool. CYC=3 (stateOk ||, instrOk, name non-empty+Limit).

2. **`DeduplicateByPrice(List<(double Price, int Qty)>)` (static)**
   - Extracted dedup dictionary foreach from `SnapshotTargetOrders`.
   - Returns initialized List (never null). CYC=2 (foreach + TryGetValue).

3. **`LogLeaderDiag(Account, List<(double Price, int Qty)>, int)` (static)**
   - Extracted the `_sb` StringBuilder + for-loop DIAG block from `Execute()`.
   - CYC=2 (for-loop). Execute()'s CCN: 9->8 by removing the DIAG for-loop branch.

4. **`IsNonTerminalForInstr(Order, Instrument)` (static)**
   - Extracts the compound null+instrOk+IsPttBeOrder+IsNonTerminalPttBeState check.
   - Used by both `WaitForPttBeCancelled` and `CancelPttBeOrders` to eliminate duplicated 4-branch pattern.
   - CYC=4.

**SnapshotTargetOrders CCN: 20->6** (null guard + foreach + IsTargetOrder call + isNative + isPtt + pttTargets.Count = 6)
**Execute() CCN: 9->8** (DIAG for-loop moved into LogLeaderDiag)
**WaitForPttBeCancelled CCN: 10->6** (foreach + IsNonTerminalForInstr call replaces 4 guards)
**CancelPttBeOrders CCN: 9->5** (foreach + IsNonTerminalForInstr call replaces 4 guards)

---

### PttBreakEven.cs -- 7 new helpers + 1 in-place rewrite

1. **`IsCancellableState(OrderState)` (static)**
   - Extracted 5-term OR stateOk from `CancelStaleBracketsLocal`.
   - CYC=5 (five || terms).

2. **`IsStaleOrder(Order, Instrument)` (static)**
   - Extracted combined IsCancellableState+instrOk+notBe filter.
   - CYC=3.

3. **`IsSnapshotEligibleState(OrderState)` (static)**
   - Extracted 5-term OR stateOk from `SnapshotTargetsLocal`.
   - CYC=5 (five || terms).

4. **`IsInvalidInput(Account, Instrument)` (static)**
   - Extracted `acc == null || instr == null` null guard from `SubmitBeStopLocal`.
   - Eliminates the `||` operator from the caller's CCN.
   - CYC=1.

5. **`SafeName(Account)` (static)**
   - Extracted `acc != null ? acc.Name : "null"` ternary from catch blocks.
   - Returns string (never null -- "null" sentinel).
   - CYC=1.

6. **`SubmitBareStop(Account, Instrument, OrderAction, double)` (static)**
   - Extracted the 0-targets path from `SubmitBeTargetsLocal`.
   - NT8-049: arg6=0, arg7=bePrice. CYC=3.

7. **`SubmitBePair(Account, Instrument, OrderAction, double, string, int, (double,int,OrderAction))` (static)**
   - Extracted per-pair stop+target OCO submit from `SubmitBeTargetsLocal` for-loop body.
   - NT8-049: stop arg6=0 arg7=bePrice; target arg6=t.Price arg7=0. CYC=3.

8. **`IsPttQxTarget` in-place rewrite (no new helper)**
   - Replaced 8-char && chain with `name.StartsWith("PTT-QX-T", Ordinal) && name[8]>='1' && name[8]<='3'`.
   - CCN: 12->5. Identical logic, no behavior change.

**CancelStaleBracketsLocal CCN: 16->6**
**SubmitBeTargetsLocal CCN: 15->4** (null guard + null guard + stopDirection + 0-targets branch + for-loop = base+4)
**SnapshotTargetsLocal CCN: 13->5**
**IsPttQxTarget CCN: 12->5** (in-place rewrite)
**SubmitBeStopLocal CCN: 9->6** (IsInvalidInput replaces ||, SafeName removes ternary)

---

### Tests/BwaveRefactorLaneCTests.cs (NEW FILE)

- 14 structural [Fact] tests using xUnit reflection.
- 1 test per extracted helper verifying existence and parameter count.
- Added to PropTraderTools.csproj `<Compile>` list.

---

## Scan Results

### SCAN-01: lock() check
```
Command: Select-String -Pattern "\block\s*\(" on all .cs files
Result: 0 actual lock() calls. All hits are in comments only.
PASS
```

### SCAN-02: Non-ASCII check (changed files)
```
Command: Get-Content changed files | Where-Object { $_ -match '[^\x00-\x7F]' }
Result: SCAN-02: 0 non-ASCII results
PASS
```

### SCAN-03: FontFamily check
```
Command: Select-String -Pattern "FontFamily"
Result: 0 FontFamily property assignments in any file. Hits are in comments only.
PASS
```

### SCAN-04: Hex literal check
```
Command: Select-String -Pattern "#[0-9A-Fa-f]{6}"
Result: 0 hex literals in C-1 changed files. Pre-existing hits in TradeCopierPanel.cs/Window.cs
        are in comments only (e.g. // green #22c55e).
PASS
```

### SCAN-05: PTT- prefix on all CreateOrder signal names
```
Verified manually -- all arg9 signal names:
  PttQuickExit: "PTT-QX-Stop", "PTT-QX-Stop{N}", "PTT-QX-T{N}"
  PttBreakEven: "PTT-BE-Stop", "PTT-BE-Stop-{N}", "PTT-BE-Target-{N}"
Result: 0 violations
PASS
```

### SCAN-06: DateTime.Now check
```
Command: Select-String -Pattern "DateTime\.Now[^U]"
Result: 0 actual DateTime.Now calls. All hits are in comment strings "No DateTime.Now".
PASS
```

### SCAN-07: Lizard CCN (Features/*.cs methods CCN > 8)
```
Command: lizard Features/*.cs --csv | ConvertFrom-Csv | Where-Object { [int]$_.CCN -gt 8 }
Result: 0 rows output
PASS
```

---

## Build Output

```
Build succeeded.
  1 Warning(s)  [pre-existing: B131Tests.cs xUnit2004 -- unrelated to C-1]
  0 Error(s)
```

---

## NT8 Sync Result

```
=== PTT SYNC: src/PropTraderTools -> NT8 AddOns ===
  COPIED:  TradeCopierPanel.cs
  COPIED:  TradeCopierWindow.cs
  COPIED:  Features\PttBreakEven.cs
  COPIED:  Features\PttGlobalQuickExit.cs
  COPIED:  Features\PttQuickExit.cs
  Copied: 5 | In-sync: 13 | Excluded: 65

=== PTT VERIFY: MD5 check every synced file ===
  OK  (18/18 files)

=== SYNC + VERIFY: PASS (18 files confirmed) ===
```

---

## CCN Post-Extraction Summary

| Method | File | CCN Before | CCN After | PASS? |
|--------|------|-----------|-----------|-------|
| Execute(Account,Instrument,int,List,bool,double,int) | PttQuickExit | 32 | 8 | YES |
| SubmitQxOcoPair | PttQuickExit | NEW | 6 | YES |
| SubmitStopOrder | PttQuickExit | NEW | 2 | YES |
| SubmitTargetOrder | PttQuickExit | NEW | 2 | YES |
| SnapshotTargetOrders | PttGlobalQuickExit | 20 | 6 | YES |
| IsTargetOrder | PttGlobalQuickExit | NEW | 3 | YES |
| DeduplicateByPrice | PttGlobalQuickExit | NEW | 2 | YES |
| Execute() | PttGlobalQuickExit | 9 | 8 | YES |
| LogLeaderDiag | PttGlobalQuickExit | NEW | 2 | YES |
| WaitForPttBeCancelled | PttGlobalQuickExit | 10 | 6 | YES |
| IsNonTerminalForInstr | PttGlobalQuickExit | NEW | 4 | YES |
| CancelPttBeOrders | PttGlobalQuickExit | 9 | 5 | YES |
| CancelStaleBracketsLocal | PttBreakEven | 16 | 6 | YES |
| IsCancellableState | PttBreakEven | NEW | 5 | YES |
| IsStaleOrder | PttBreakEven | NEW | 3 | YES |
| SubmitBeTargetsLocal | PttBreakEven | 15 | 4 | YES |
| SubmitBareStop | PttBreakEven | NEW | 3 | YES |
| SubmitBePair | PttBreakEven | NEW | 3 | YES |
| SnapshotTargetsLocal | PttBreakEven | 13 | 5 | YES |
| IsSnapshotEligibleState | PttBreakEven | NEW | 5 | YES |
| IsPttQxTarget | PttBreakEven | 12 | 5 | YES (in-place rewrite) |
| SubmitBeStopLocal | PttBreakEven | 9 | 6 | YES |
| IsInvalidInput | PttBreakEven | NEW | 1 | YES |
| SafeName | PttBreakEven | NEW | 1 | YES |

---

## Verification Checklist

- [x] dotnet build: 0 errors, 0 new warnings
- [x] SCAN-01 (lock): 0 hits
- [x] SCAN-02 (non-ASCII): 0 hits in changed files
- [x] SCAN-03 (FontFamily): 0 hits in changed files
- [x] SCAN-04 (hex literals): 0 hits in changed files
- [x] SCAN-05 (PTT- prefix): 0 violations
- [x] SCAN-06 (DateTime.Now): 0 hits
- [x] SCAN-07 (lizard CCN): 0 rows CCN > 8 in Features/*.cs
- [x] NT8 sync: 18/18 OK, 0 MISMATCH
- [x] No public/internal signature changes
- [x] No logic deleted
- [x] No lock(), async void, return null (new helpers), non-ASCII identifiers
- [x] CopyEngine.cs, TradeCopierPanel.cs, TradeCopierWindow.cs NOT touched
- [x] Ticket C-2 files NOT touched
- [x] [SCOPE LOCK CONFIRMED: Ticket C-1 only]

---

**BUILD_PASS**
