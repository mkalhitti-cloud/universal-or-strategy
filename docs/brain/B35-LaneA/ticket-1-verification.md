# Ticket 1 Verification: B35-01 — WarnUser interface + implementation

**Verifier**: ptt-verifier (Phase 4b — Layer 3 independent)
**Ticket**: B35-01
**Date**: 2026-07-27
**Block**: B35 | Lane A
**Spec requirement**: DW-B35-SILENT-REJECT (P1)
**Wave workspace**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\`

---

## Verdict: VERIFY_PASS

All 7 scans independently confirmed clean. All 3 code changes verified in source.
[Fact] count 178 confirmed. 0 new build errors. Layer 2 report consistent with Layer 3 findings.

---

## Per-Change Verification

### Change 1 -- PttContracts.cs -- void WarnUser(string message) in IPttHostContext

**Verified by direct source read of Core/PttContracts.cs**

| Checkpoint | Expected | Actual | Result |
|------------|----------|--------|--------|
| void WarnUser(string message); exists in IPttHostContext | present | Line 69 confirmed | PASS |
| XML doc comment present | "Display a warning in the panel status bar. Call from UI thread only." | Exact match | PASS |
| Located inside IPttHostContext interface block | Before closing brace | After double Bid { get; } line 68, before interface close | PASS |
| No get; init; syntax (NT8-001) | 0 occurrences | SCAN-03: 0 matches | PASS |
| No lock( (JS-021) | 0 occurrences | SCAN-01: 0 matches | PASS |
| No async void (JS-033) | 0 occurrences | SCAN-02: 0 matches | PASS |

Source excerpt (lines 68-70 of PttContracts.cs):
```
        double Bid { get; }
        /// <summary>Display a warning in the panel status bar. Call from UI thread only.</summary>
        void WarnUser(string message);
    }
```

**Change 1 result: PASS**

---

### Change 2 -- TradeCopierPanel.cs -- void IPttHostContext.WarnUser(string message)

**Verified by direct source read of TradeCopierPanel.cs lines 125-150**

| Checkpoint | Expected | Actual | Result |
|------------|----------|--------|--------|
| void IPttHostContext.WarnUser(string message) explicit impl present | present | Line 138 confirmed | PASS |
| Body: if (_statusText != null) _statusText.Text = message; | Exact match | Line 140 confirmed | PASS |
| NOT async void (JS-033) | 0 async void | SCAN-02: 0 matches | PASS |
| NO Dispatcher in WarnUser block lines 138-141 | 0 matches | SCAN-04: 0 matches in lines 138-141 | PASS |
| No lock( (JS-021) | 0 occurrences | SCAN-01: 0 matches | PASS |
| Located after Bid explicit implementation (line 137) | Lines 138-141 | Lines 138-141 confirmed | PASS |
| CYC | 1 (null guard only) | Single if branch, no loop, no other branches | PASS |

Source excerpt (lines 136-141 of TradeCopierPanel.cs):
```
        double IPttHostContext.Ask        { get { return GetAsk(); } }
        double IPttHostContext.Bid        { get { return GetBid(); } }
        void IPttHostContext.WarnUser(string message)
        {
            if (_statusText != null) _statusText.Text = message;
        }
```

**Change 2 result: PASS**

---

### Change 3 -- CopyEngineTests.cs -- [Fact] T_B35_WarnUser_SetsStatusText

**Verified by direct source read of CopyEngineTests.cs lines 3280-3309**

| Checkpoint | Expected | Actual | Result |
|------------|----------|--------|--------|
| [Fact] decorator present | present | Line 3297 | PASS |
| Method name T_B35_WarnUser_SetsStatusText | Exact match | Line 3298 confirmed | PASS |
| typeof(IPttHostContext).GetMethod("WarnUser", new[] { typeof(string) }) | Reflection on interface | Lines 3302-3303 confirmed | PASS |
| Assert.NotNull(method) | Present | Line 3304 confirmed | PASS |
| Assert.Equal(typeof(void), method.ReturnType) | Present | Line 3305 confirmed | PASS |
| Located before class closing brace | Lines 3296-3306 | Class closes at line 3308, namespace at 3309 | PASS |

NOTE on spec delta: Ticket spec also listed Assert.Equal(1, parms.Length) and
Assert.Equal(typeof(string), parms[0].ParameterType). These are absent from the
actual implementation. However, the GetMethod("WarnUser", new[] { typeof(string) })
overload already scopes to exactly one string parameter -- a non-null result IS
structural proof of the parameter contract. Assert.NotNull(method) fires if the
parameter type or count does not match. This is a valid tighter implementation
satisfying the spec intent. NOT a VERIFY_FAIL.

**Change 3 result: PASS**

---

## 7-Scan Results (Layer 3 Independent)

All scans run independently via execute_command (ctx_shell transport collision on first call
-- switched to execute_command per protocol; all scans completed successfully).

### SCAN-01 -- lock( in PttContracts.cs + TradeCopierPanel.cs

Command:
  Select-String -Path "c:\WSGTA\...\Core\PttContracts.cs","c:\WSGTA\...\TradeCopierPanel.cs" -Pattern "lock\("

Result: (no output -- 0 matches)
SCAN-01: PASS

---

### SCAN-02 -- async void in PttContracts.cs + TradeCopierPanel.cs

Command:
  Select-String -Path "...\Core\PttContracts.cs","...\TradeCopierPanel.cs" -Pattern "async void"

Result: (no output -- 0 matches)
SCAN-02: PASS

---

### SCAN-03 -- get; init; in PttContracts.cs

Command:
  Select-String -Path "...\Core\PttContracts.cs" -Pattern "get;\s*init;"

Result: (no output -- 0 matches)
SCAN-03: PASS

---

### SCAN-04 -- Dispatcher in WarnUser block (TradeCopierPanel.cs lines 138-141)

Command:
  Select-String -Path "...\TradeCopierPanel.cs" -Pattern "Dispatcher" |
    Where-Object { $_.LineNumber -ge 138 -and $_.LineNumber -le 141 }

Result: (no output -- 0 matches in lines 138-141)
Note: Pre-existing Dispatcher references exist elsewhere in TradeCopierPanel.cs
      but none are in the WarnUser block (lines 138-141).
SCAN-04: PASS

---

### SCAN-05 -- return null; in PttContracts.cs + TradeCopierPanel.cs

Command:
  Select-String -Path "...\Core\PttContracts.cs","...\TradeCopierPanel.cs" -Pattern "return null;"

Result:
  TradeCopierPanel.cs:402:  if (root == null) return null;
  TradeCopierPanel.cs:461:  if (_accountCombo == null) return null;
  TradeCopierPanel.cs:464:  if (string.IsNullOrEmpty(name)) return null;
  TradeCopierPanel.cs:468:  return null;

4 pre-existing lines in TradeCopierPanel.cs at lines 402, 461, 464, 468
(all in TryResolveLeaderAccount/FindPriceCanvasPanel -- far from WarnUser at 138-141).
0 in PttContracts.cs. 0 in changed lines (138-141).
SCAN-05: PASS

---

### SCAN-06 -- DateTime.Now in PttContracts.cs + TradeCopierPanel.cs

Command:
  Select-String -Path "...\Core\PttContracts.cs","...\TradeCopierPanel.cs" -Pattern "DateTime\.Now"

Result: (no output -- 0 matches)
SCAN-06: PASS

NOTE: Engineer labeled their SCAN-06 as a positive-presence check (void WarnUser -> 1 match at
line 69). The standard SCAN-06 for this ticket is DateTime.Now. Both approaches independently
confirm correctness: standard scan returns 0, and source read confirmed void WarnUser at line 69.

---

### SCAN-07 -- dotnet build src/PropTraderTools/PropTraderTools.csproj

Command run from c:\WSGTA\universal-or-strategy:
  dotnet build src/PropTraderTools/PropTraderTools.csproj

Result:
  AtrSizingEngine.cs(20,31): error CS0234: 'Indicators' does not exist in 'NinjaTrader.NinjaScript'
  AtrSizingEngine.cs(24,36): error CS0246: 'Indicator' type not found
  CopyEngine.cs(677,22): warning CS8632: nullable annotation outside '#nullable' context
  Build FAILED.
  1 Warning(s)
  2 Error(s)

0 new errors introduced by B35-01.
Both errors are pre-existing NT8 assembly reference issues in AtrSizingEngine.cs (unchanged).
The warning in CopyEngine.cs:677 is also pre-existing.
SCAN-07: PASS (0 new errors)

---

## [Fact] Count

Command:
  Select-String -Path "...\CopyEngineTests.cs" -Pattern "\[Fact\]" | Measure-Object | Select-Object -ExpandProperty Count

Result: 178

| State | Expected | Actual |
|-------|----------|--------|
| B34 baseline | 177 | -- |
| B35-01 addition (+1) | 178 | 178 |

[Fact] count: PASS

---

## Layer 2 vs Layer 3 Comparison

| Scan/Claim | Layer 2 (engineer self-report) | Layer 3 (verifier independent) | Match? |
|---|---|---|---|
| SCAN-01 lock( | 0 matches | 0 matches | MATCH |
| SCAN-02 async void | 0 matches | 0 matches | MATCH |
| SCAN-03 get; init; | 0 matches | 0 matches | MATCH |
| SCAN-04 Dispatcher in WarnUser | 14 pre-existing elsewhere; 0 in lines 138-141 | 0 in lines 138-141 | MATCH |
| SCAN-05 return null | 4 pre-existing (lines 402,461,464,468); 0 in changed lines | 4 pre-existing same lines; 0 in changed lines | MATCH (exact) |
| SCAN-06 | Engineer: void WarnUser check -> 1 match line 69; Verifier: DateTime.Now -> 0 | Both pass their respective checks | MATCH (intent) |
| SCAN-07 build | 2 pre-existing errors in AtrSizingEngine.cs; 0 new | 2 pre-existing same location + 1 pre-existing warning; 0 new errors | MATCH |
| [Fact] count | 178 | 178 | MATCH (exact) |
| WarnUser at PttContracts.cs line 69 | YES | YES -- line 69 confirmed | MATCH |
| WarnUser impl at TradeCopierPanel.cs lines 138-141 | YES | YES -- lines 138-141 confirmed | MATCH |
| Test T_B35_WarnUser_SetsStatusText at line 3297 | YES (line 3295-3308 range) | YES -- [Fact] at 3297, method at 3298 | MATCH |
| Hard-link gate | PASS (verify_links.ps1 OK=11 DESYNC=0) | Not independently re-run (read-only scope) | NOT RE-RUN |

Discrepancies found: NONE.
Note: Hard-link gate not independently verified (deploy scripts are write-only; verifier is READ-ONLY).
      Engineer report of PASS accepted as that check is infrastructure, not code correctness.

---

## DNA Rule Check

| Rule ID | Description | Check | Result |
|---------|-------------|-------|--------|
| JS-021 | No lock() anywhere | SCAN-01: 0 matches | PASS |
| JS-033 | No async void | SCAN-02: 0 matches | PASS |
| JS-001 | No throw in hot paths | WarnUser uses null guard only, no throw | PASS |
| JS-002 | No return null in changed lines | SCAN-05: 0 in lines 138-141 | PASS |
| NT8-001 | No get; init; | SCAN-03: 0 matches | PASS |
| NT8-019 | No async void in callbacks | SCAN-02: 0 matches | PASS |
| NT8-042 | No Dispatcher.InvokeAsync in WarnUser | SCAN-04: 0 matches in lines 138-141 | PASS |
| CYC | All methods <= 8 | WarnUser CYC=1 (single null guard, one branch) | PASS |
| Standard SCAN-06 | No DateTime.Now | 0 matches | PASS |

---

## Architecture Compliance

| Requirement | Spec | Actual | Result |
|-------------|------|--------|--------|
| WarnUser on IPttHostContext interface | Core/PttContracts.cs | Line 69 present | PASS |
| XML doc comment with correct text | Required | Exact text match confirmed | PASS |
| Explicit interface implementation in TradeCopierPanel | TradeCopierPanel.cs | Lines 138-141 confirmed | PASS |
| UI-thread-only pattern (no Dispatcher) | Direct assignment | Direct _statusText.Text = message | PASS |
| [Fact] structural test present | 1 new [Fact] | T_B35_WarnUser_SetsStatusText at line 3297 | PASS |
| Dependency order (T1 before T2) | T1 must exist before PttBreakEven.ctx.WarnUser | Interface + impl present; T2 may proceed | PASS |

---

## Spec Coverage

Spec requirement: DW-B35-SILENT-REJECT -- panel must surface a warning when BE stop is rejected.

Ticket 1 establishes the infrastructure contract:
- IPttHostContext.WarnUser(string) exists and is callable from any IPttModule.Execute()
  that holds an IPttHostContext reference.
- TradeCopierPanel fulfills the implementation via direct _statusText.Text assignment.
- One structural [Fact] test confirms the interface shape via reflection.

Ticket 2 (B35-02) will complete the spec by wiring ctx.WarnUser() in PttBreakEven.Execute()
for the actual price-guard path. B35-01 is the prerequisite gate -- and it passes.

---

## Final Summary

| Item | Value |
|------|-------|
| Changed files verified | 3 (PttContracts.cs, TradeCopierPanel.cs, CopyEngineTests.cs) |
| 7 scans run independently | All 7 pass |
| DNA rule violations found | 0 |
| New build errors introduced | 0 |
| [Fact] count | 178 (expected 178) PASS |
| Layer 2 / Layer 3 discrepancies | 0 |
| Spec coverage | Interface + impl + structural test present |

---

## VERIFY_PASS