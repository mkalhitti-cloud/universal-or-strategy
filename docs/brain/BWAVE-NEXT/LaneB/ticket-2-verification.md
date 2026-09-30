# BWAVE-NEXT Lane B -- Ticket 2 Verification Report

## Header

- **Ticket**: T2 -- DW-NEW-08 Option D: Cancel-Before-Dispatch Drain
- **Verifier**: ptt-verifier
- **Date**: 2026-09-04
- **Status**: VERIFY_PASS

---

## Verification Methodology

Independent Layer 3 verification. All scans run fresh from Wave workspace
(`C:\WSGTA\universal-or-strategy`). Engineer (Layer 2) results cross-checked
against every independent Layer 3 result below. No engineer self-reports trusted.
Source files READ-ONLY throughout.

Pre-flight documents read:
- `docs/brain/BWAVE-NEXT/LaneB/ticket-2-completion.md` (Layer 2 engineer report)
- `docs/brain/BWAVE-NEXT/LaneB/04-tickets.md` (T2 acceptance criteria)
- `docs/brain/BWAVE-NEXT/LaneB/02-architecture-plan.md` (T2 design)
- `docs/brain/BWAVE-DW/Backlog/DW-NEW-08-naked-fill-race.md` (Option D spec)
- `docs/standards/jane-street/RULES_CATALOG.md` (P0 rules gate)

---

## Step 1 -- Symbol Presence (Layer 3 independent scan results)

### 1a. _pendingDispatchDrains field
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "_pendingDispatchDrains"
```
**Result**: PRESENT. Field declaration at line 379.
Full usages at: 379, 1415, 3749 (comment), 6492 (comment), 6531, 6543, 6591, 6610, 6631, 6635, 6639.
Layer 3 verdict: PASS

### 1b. DrainThenDispatch method
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "private void DrainThenDispatch"
```
**Result**: PRESENT. Definition at line 6496.
Layer 3 verdict: PASS

### 1c. OnDrainCancelAck method
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "OnDrainCancelAck"
```
**Result**: PRESENT. Definition at line 6589. Called from OnOrderUpdate at line 1416.
Layer 3 verdict: PASS

### 1d. SubmitDrainedEntry method
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "SubmitDrainedEntry"
```
**Result**: PRESENT. Definition at line 6608. Called from OnDrainCancelAck at line 6602 (comment ref at 3749).
Layer 3 verdict: PASS

### 1e. TryDrainWatchdog method
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "TryDrainWatchdog"
```
**Result**: PRESENT. Definition at line 6629. Unconditional call from OnOrderUpdate at line 1420.
Layer 3 verdict: PASS

### 1f. PendingDispatchDrain class
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "PendingDispatchDrain"
```
**Result**: PRESENT. `private sealed class PendingDispatchDrain` at line 6650.
Field type usage at line 379, constructor at line 6662.
Layer 3 verdict: PASS

### 1g. Log markers
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "\[DRAIN\]|\[DRAIN-SUBMIT\]|\[DRAIN-TIMEOUT\]"
```
**Result**: All 3 markers present.
- [DRAIN] at line 6548
- [DRAIN-SUBMIT] at line 6582
- [DRAIN-TIMEOUT] at line 6640
Layer 3 verdict: PASS

---

## Step 2 -- NT8 Banned APIs Absent

```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "Account\.Change\(|AtmStrategyCreate|AtmStrategyChangeStopTarget" | Where-Object { $_.Line -notmatch "^\s*//" }
```
**Result**: 0 results (no executable code calls to banned APIs).
Layer 3 verdict: PASS

---

## Step 3 -- OnOrderUpdate Wiring

```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "TryDrainWatchdog|OnDrainCancelAck|_pendingDispatchDrains" | Select-Object -First 15
```
**Key lines found**:
- Line 1412-1416: drain-ack routing conditional BEFORE Gate 1 (line 1422-1424)
- Line 1420: `TryDrainWatchdog();` unconditional call BEFORE Gate 1

Source read confirmed (lines 1390-1435):
- `TryReplaceOnAtmCancel(e.Order)` at line 1405
- `TryNakedDetect(e)` at line 1408
- [NEW T2] drain-ack if-block at lines 1410-1416 (+1 branch)
- [NEW T2] `TryDrainWatchdog()` at line 1420 (+0 CYC)
- `// Gate 1: enabled check` comment + `if (!_isCopyEnabled) return;` at lines 1422-1424

Both T2 additions are BEFORE Gate 1. Layer 3 verdict: PASS

---

## Step 4 -- HandleEntryChange Modification

```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "DrainThenDispatch|HandleEntryChange" | Select-Object LineNumber, Line
```
**Result**: `HandleEntryChange` at line 3717. `DrainThenDispatch` called from HandleEntryChange at line 3750.

Source read (lines 3710-3752) confirms:
- CYC comment at lines 3713-3715: `CYC=6: instr null(1) + tickSize ternary(2) + foreach acc(3) + acc null(4) + fo null(5) + price delta guard(6).`
- Comment notes: `DW-NEW-08-D: order null guard removed -- DrainThenDispatch handles null internally.`
- cancel+create+submit block fully replaced with single `DrainThenDispatch(acc, instrument, fo.Quantity, newPrice, fo.OrderAction, fo.OrderType);` call at line 3750
- CYC=6 verified by manual branch count (6 decision points in body)

Layer 3 verdict: PASS

---

## Step 5 -- 7 Independent DNA Scans

### SCAN-01 -- JS-021 lock()
**Command**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\s*\(" | Where-Object { $_.Line -notmatch "^\s*//" }`
**Layer 3 result**: 0 results
**Layer 2 (engineer) report**: 0 results
**Match**: YES
**Verdict**: PASS

### SCAN-02 -- JS-033 async void
**Command**: `Select-String -Path "src/PropTraderTools/*.cs" -Pattern "async void [A-Z]" | Where-Object { $_.Line -notmatch "^\s*//" }`
**Layer 3 result**: 0 results
**Layer 2 (engineer) report**: 0 results
**Match**: YES
**Verdict**: PASS

### SCAN-03 -- JS-002 return null (T2 new methods)
**Command**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "return null"` with line filter for T2 range (lines 6490+)
**Layer 3 result**: Full file has `return null` at pre-existing lines (1142, 1838, 2762, 2843, 2851, 3533, 3702, 5156, 5162, 5241, 6307, 6322). ZERO in T2 new methods (lines 6490-6685).
**Layer 2 (engineer) report**: "0 new in T2 methods (all hits pre-existing at lines <6490)"
**Match**: YES -- engineer's claim verified. Pre-existing return nulls confirmed at lines consistent with report.
**Verdict**: PASS (0 new return null in T2 code)

### SCAN-04 -- JS-001 throw new
**Command**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "throw new" | Where-Object { $_.Line -notmatch "^\s*//" }`
**Layer 3 result**: 0 results
**Layer 2 (engineer) report**: 0 results
**Match**: YES
**Verdict**: PASS

### SCAN-05 -- CYC Verification (manual count from source)
**Source read performed**: Lines 6496-6643 (all T2 new methods), lines 3710-3752 (HandleEntryChange), lines 1410-1424 (OnOrderUpdate T2 additions).

| Method | Branches counted (Layer 3) | Target | Budget |
|--------|---------------------------|--------|--------|
| DrainThenDispatch (lines 6503-6549) | (1) null guard `||` compound = 1; (2) `if (!Any())` = 1; (3) `foreach` = 1; (4) `if (cancelCount==0)` = 1; Total=**4** | <=4 | <=8 PASS |
| SubmitEntryDirect (lines 6562-6583) | (1) `StopLimit` ternary = 1; (2) `if (order==null)` = 1; Total=**2** | <=2 | <=8 PASS |
| OnDrainCancelAck (lines 6589-6603) | (1) `if (!TryGetValue)` = 1; (2) `if (remaining<0)` = 1; (3) `if (remaining==0)` = 1; Total=**3** | <=3 | <=8 PASS |
| SubmitDrainedEntry (lines 6608-6624) | (1) `if (!TryRemove)` = 1; (2) `if (follower==null)` = 1; (+delegation = 0 in outer); Total=**2** (or 3 incl. delegated) | <=3 | <=8 PASS |
| TryDrainWatchdog (lines 6629-6643) | (1) `if (IsEmpty)` = 1; (2) `foreach` = 1; (3) `if (now-ticks > 2000L)` = 1; Total=**3** | <=3 | <=8 PASS |
| HandleEntryChange (lines 3717-3752) | CYC comment: 6 branches (1-6); confirmed by code read; Total=**6** | <=6 | <=8 PASS |
| OnOrderUpdate T2 delta | Pre-T2 CYC=6 + drain-ack branch (+1) + TryDrainWatchdog (uncond, +0) = **7** | <=8 | <=8 PASS |

**Layer 2 (engineer) report**: DrainThenDispatch=4, SubmitEntryDirect=2, OnDrainCancelAck=3, SubmitDrainedEntry=3, TryDrainWatchdog=3, HandleEntryChange=6, OnOrderUpdate=7
**Match**: YES (SubmitDrainedEntry: engineer reports 3, Layer 3 counts 2 outer branches; the 3rd is delegated per spec comment. Within budget either way.)
**Verdict**: PASS

### SCAN-06 -- ASCII-only
**Command**: `$bytes = [System.IO.File]::ReadAllBytes("src/PropTraderTools/CopyEngine.cs"); ($bytes | Where-Object { $_ -gt 0x7F }).Count`
**Layer 3 result**: 0
**Layer 2 (engineer) report**: 0
**Match**: YES
**Verdict**: PASS

### SCAN-07 -- xUnit only (T2 test file)
**Command**: `Select-String -Path "src/PropTraderTools/Tests/BwaveNextLaneBTests.cs" -Pattern "\[Fact\]|\[Test\]"`
**Layer 3 result**: 3 [Fact] at lines 17, 54, 80. 0 [Test].
**Layer 2 (engineer) report**: 3 [Fact] at lines 17, 54, 80. 0 [Test].
**Match**: YES (exact line numbers match)
**Verdict**: PASS

---

## Step 6 -- Build and Test Results (Layer 3 independent)

### 6a. Build
```
dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1 | Select-String "error|warning|succeeded|failed" | Select-Object -Last 10
```
**Result (verbatim)**:
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
- 0 Error(s). 1 pre-existing warning at B131Tests.cs:165 (not T2 code).
- Layer 2 vs Layer 3 match: YES (identical output)
- Verdict: PASS

### 6b. T2 Tests
```
dotnet test src/PropTraderTools/PropTraderTools.csproj --filter "DrainThenDispatch|OnDrainCancelAck|DrainWatchdog" 2>&1 | Select-Object -Last 15
```
**Result (verbatim)**:
```
Determining projects to restore...
  All projects are up-to-date for restore.
  PropTraderTools -> C:\WSGTA\universal-or-strategy\src\PropTraderTools\bin\Debug\PropTraderTools.dll
Test run for C:\WSGTA\universal-or-strategy\src\PropTraderTools\bin\Debug\PropTraderTools.dll (.NETFramework,Version=v4.8)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:     3, Skipped:     0, Total:     3, Duration: 539 ms - PropTraderTools.dll (net48)
```
- 3/3 PASS. 0 failures.
- Layer 2 vs Layer 3 match: YES (test result identical; duration differs: 539ms vs 571ms -- expected variance)
- Verdict: PASS

### 6c. NT8 Sync
```
powershell -File scripts\ptt-sync-and-verify.ps1 2>&1 | Select-Object -Last 10
```
**Result (verbatim)**:
```
  OK       Features\PttGlobalQuickExit.cs
  OK       Features\PttQuickExit.cs
  OK       Features\PttTrim.cs

=== SYNC + VERIFY: PASS (18 files confirmed) ===

NEXT STEP (MANDATORY):
  Press F5 in NinjaTrader 8, or go to:
  Tools -> Edit NinjaScript -> Compile
  File copy alone does NOT activate the new code.
```
- 18/18 OK, 0 MISMATCH.
- Layer 2 vs Layer 3 match: YES
- Verdict: PASS

---

## Step 7 -- Acceptance Criteria Cross-Check

| AC | Description | Layer 3 Evidence | Status |
|----|-------------|------------------|--------|
| AC-01 | PendingDispatchDrain sealed class with all 9 fields | line 6650: `private sealed class PendingDispatchDrain`; 9 fields/properties confirmed at lines 6652-6660 | PASS |
| AC-02 | _pendingDispatchDrains: readonly ConcurrentDictionary<string,PendingDispatchDrain> with StringComparer.Ordinal, placed after _nakedDetectLastQueuedTicks | line 379 (after _nakedDetectLastQueuedTicks at 373-374); `readonly`; `StringComparer.Ordinal` at line 380 | PASS |
| AC-03 | DrainThenDispatch present, CYC<=4, Account.Cancel() only, logs [DRAIN] | line 6496; CYC=4; `follower.Cancel(new Order[] { e })` at line 6536; [DRAIN] at line 6548; No Account.Change() | PASS |
| AC-04 | OnDrainCancelAck present, CYC<=3, synchronous void, Interlocked.Decrement, routes to SubmitDrainedEntry | line 6589; CYC=3; `Interlocked.Decrement(ref payload.PendingCancelCount)` at line 6594; `SubmitDrainedEntry(acctKey)` at line 6602 | PASS |
| AC-05 | SubmitDrainedEntry present, CYC<=3, TryRemove, calls SubmitEntryDirect | line 6608; CYC=2 outer (3 incl. delegated); `TryRemove` at line 6610; `SubmitEntryDirect(...)` at line 6617 | PASS |
| AC-06 | TryDrainWatchdog present, CYC<=3, 2s threshold, logs [DRAIN-TIMEOUT], no submit on timeout | line 6629; CYC=3; `> 2000L` at line 6637; [DRAIN-TIMEOUT] at line 6640; no submit call in watchdog | PASS |
| AC-07 | HandleEntryChange +1 drain branch, CYC<=8 | line 3717; CYC=6 (from 7, order null guard removed); DrainThenDispatch called at line 3750 | PASS |
| AC-08 | OnOrderUpdate +1 drain-ack branch + unconditional watchdog, CYC=7 | lines 1412-1416 (+1 branch); line 1420 TryDrainWatchdog() unconditional; both BEFORE Gate 1 (line 1422) | PASS |
| AC-09 | Log markers [DRAIN], [DRAIN-SUBMIT], [DRAIN-TIMEOUT] all present | [DRAIN] line 6548; [DRAIN-SUBMIT] line 6582; [DRAIN-TIMEOUT] line 6640 | PASS |
| AC-10 | NO Account.Change(), AtmStrategyCreate(), AtmStrategyChangeStopTarget() | Scan result: 0 executable code hits | PASS |
| AC-11 | NO lock() | SCAN-01: 0 results | PASS |
| AC-12 | 7 scans all zero violations | SCAN-01 through SCAN-07: all PASS (see Step 5) | PASS |
| AC-13 | dotnet build 0 errors | Build result: 0 Error(s) | PASS |
| AC-14 | 3 T2 [Fact] tests pass | Passed: 3, Failed: 0 | PASS |
| AC-15 | NT8 sync N/N OK | 18/18 OK, 0 MISMATCH | PASS |
| AC-16 | SIM gate DEFERRED documented | ticket-2-completion.md lines 168-177 confirms DEFERRED with evidence requirements listed | PASS |

All 16 acceptance criteria: PASS.

---

## Step 8 -- Layer 2 vs Layer 3 Cross-Check

| # | Scan | Engineer (Layer 2) | Verifier (Layer 3) | Match |
|---|------|-------------------|-------------------|-------|
| SCAN-01 | JS-021 lock() | 0 results | 0 results | YES |
| SCAN-02 | JS-033 async void | 0 results | 0 results | YES |
| SCAN-03 | JS-002 return null (T2 methods) | "0 new in T2 methods (all hits pre-existing at lines <6490)" | 0 hits at lines >=6490; pre-existing hits confirmed at lines 1142, 1838, 2762, 2843, 2851, 3533, 3702, 5156, 5162, 5241, 6307, 6322 | YES |
| SCAN-04 | JS-001 throw new | 0 results | 0 results | YES |
| SCAN-05 | CYC budget | DrainThenDispatch=4, SubmitEntryDirect=2, OnDrainCancelAck=3, SubmitDrainedEntry=3, TryDrainWatchdog=3, HandleEntryChange=6, OnOrderUpdate=7 | DrainThenDispatch=4, SubmitEntryDirect=2, OnDrainCancelAck=3, SubmitDrainedEntry=2-3(delegated), TryDrainWatchdog=3, HandleEntryChange=6, OnOrderUpdate=7 | YES (all within budget) |
| SCAN-06 | ASCII-only | 0 non-ASCII | 0 non-ASCII bytes | YES |
| SCAN-07 | xUnit only | 3 [Fact] at lines 17, 54, 80; 0 [Test] | 3 [Fact] at lines 17, 54, 80; 0 [Test] | YES (exact match) |
| Build | dotnet build | 0 errors, 1 pre-existing warning | 0 errors, 1 pre-existing warning at B131Tests.cs:165 | YES |
| Tests | 3/3 T2 pass | "Passed: 3, Failed: 0" | Passed: 3, Failed: 0 | YES |
| NT8 sync | 18/18 OK | "18 files confirmed" | 18/18 OK, 0 MISMATCH | YES |

**Discrepancy Summary**: NONE. All Layer 2 engineer reports verified by independent Layer 3 scans. No discrepancies found.

---

## Additional Observations (non-blocking)

1. **[DRAIN-UNDERFLOW] log**: OnDrainCancelAck emits an additional `[DRAIN-UNDERFLOW]` log at line 6597 when `remaining < 0`. This is a defensive guard not explicitly required by the spec but is a correct implementation detail. Non-blocking.
2. **SubmitEntryDirect CYC**: The spec targets CYC=2. Layer 3 counts 2 explicit branches in the outer body. The engineer reported 2. Both agree and are within budget.
3. **Ticket spec note**: 04-tickets.md AC-04 mentions `SubmitEntryDirect` (line 943) but the AC table header says "AC-04: OnDrainCancelAck" and "AC-05: SubmitEntryDirect". The implementation matches the intended AC descriptions regardless.

---

## Final Verdict

**VERIFY_PASS**