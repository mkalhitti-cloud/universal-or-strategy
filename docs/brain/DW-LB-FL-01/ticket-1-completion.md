# DW-LB-FL-01 Ticket 1 Completion Report

**Ticket ID**: DW-LB-FL-01-T1
**Title**: Guard NakedPositionDetector dispatch with bracket-arming check
**Engineer**: ptt-engineer (Phase 4a)
**Date**: 2026-09-08
**SCOPE**: DW-LB-FL-01-T1 ONLY

---

## Changes Made

### CHANGE 1 — NEW METHOD: `FlattenIfNotArming`

**File**: `src/PropTraderTools/CopyEngine.cs`
**Placement**: Inserted before `FlattenOneAccount` at L5167 (post-insertion L5167-5185)
**Visibility**: `private void` (DW-LB-FL-01 visibility comment)
**CYC**: 2 (base(1) + HasArmingAtmBrackets branch(1))

```csharp
private void FlattenIfNotArming(Account acct, Instrument instr) // DW-LB-FL-01 visibility
{
    if (HasArmingAtmBrackets(acct, instr))
    {
        StatusUpdate?.Invoke(acct.Name + ": flat-guard: bracket-arm skip");
        return;
    }
    FlattenOneAccount(acct, instr);
}
```

### CHANGE 2 — NEW METHOD: `HasArmingAtmBrackets`

**File**: `src/PropTraderTools/CopyEngine.cs`
**Placement**: Inserted after closing brace of `HasInflightFlatten` (post-insertion L5254-5282)
**Visibility**: `internal static bool` (DW-LB-FL-01 visibility comment)
**CYC**: 5 (base(1)+foreach(1)+instr-skip(1)+stateActive-branch(1)+IsAtmBracketName(1))

```csharp
internal static bool HasArmingAtmBrackets(Account acc, Instrument instr) // DW-LB-FL-01 visibility
{
    foreach (var o in acc.Orders.ToList())
    {
        if (o.Instrument?.FullName != instr.FullName)
            continue;
        bool stateActive =
            o.OrderState == OrderState.Working
            || o.OrderState == OrderState.Submitted
            || o.OrderState == OrderState.Accepted
            || o.OrderState == OrderState.TriggerPending;
        if (!stateActive)
            continue;
        if (IsAtmBracketName(o.Name))
            return true;
    }
    return false;
}
```

### CHANGE 3 — 1-LINE EDIT: `NakedPositionDetector`

**File**: `src/PropTraderTools/CopyEngine.cs`
**Line**: L7241 (post-insertion)
**Diff**:
```diff
-                    FlattenOneAccount(acct, instr)
+                    FlattenIfNotArming(acct, instr)
```
**CYC impact**: Zero. Identifier change only — no branch added.

---

## Preserved Guards (verified)

| Guard | Pattern | Lines | Status |
|-------|---------|-------|--------|
| DW-B65-01 bypass | `DW-B65-01` | L2355, L4670, L4689, L5174 | PRESENT |
| DW-LB-FL-02 guard | `IsNativeExitOnFlatLeader` | L4674, L4681, L4710 | PRESENT |
| In-flight guard | `HasInflightFlatten` | L5221, L5224, L5240 | PRESENT |
| `FlattenOneAccount` | NOT changed | L5203 | UNCHANGED |
| `IsAccountFlattenable` | NOT changed | L5222 | UNCHANGED |

---

## 7-SCAN RESULTS

### SCAN-01: lock() check

**Command**: `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "lock\s*\(" | Select-Object LineNumber, Line`

**Result**: All matches are in comments (e.g., `// JS-021: no lock`). Zero actual `lock()` keyword usage in new or modified code.

**SCAN-01: PASS** (0 lock() in new/modified code)

---

### SCAN-02: CYC check (manual McCabe)

| Method | CYC | Calculation | Status |
|--------|-----|-------------|--------|
| `HasArmingAtmBrackets` | 5 | base(1)+foreach(1)+instr-skip(1)+stateActive-branch(1)+IsAtmBracketName(1) | PASS <= 8 |
| `FlattenIfNotArming` | 2 | base(1)+HasArmingAtmBrackets branch(1) | PASS <= 8 |
| `NakedPositionDetector` (modified) | unchanged <=6 | 1-line identifier change, zero branch delta | PASS <= 8 |

**SCAN-02: PASS** (all new/modified methods CYC <= 8)

---

### SCAN-03: ASCII-only check

**Command**: `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "[^\x00-\x7F]"` (filtered to new/modified line ranges)

**New/modified regions checked**:
- L5167-5185 (`FlattenIfNotArming`): 0 non-ASCII
- L5254-5282 (`HasArmingAtmBrackets`): 0 non-ASCII
- L7241 (`NakedPositionDetector` lambda): 0 non-ASCII

**SCAN-03: PASS** (0 non-ASCII characters in new/modified code)

---

### SCAN-04: NT8 API check

**Command**: `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "Account.Change|AtmStrategyCreate|AtmStrategyChangeStopTarget"` (filtered to new/modified lines)

**Result**: 0 matches in `HasArmingAtmBrackets`, `FlattenIfNotArming`, or `NakedPositionDetector`.

**Guards verification**:
- `IsNativeExitName`: present at L2358 — CONFIRMED
- `IsNativeExitOnFlatLeader`: present at L4674, L4710 — CONFIRMED

**SCAN-04: PASS** (0 banned NT8 APIs in new code; both guards present)

---

### SCAN-05: Build gate

**Command**: `dotnet build src/PropTraderTools/PropTraderTools.csproj --no-restore`

**Output**:
```
PropTraderTools -> C:\WSGTA\universal-or-strategy\src\PropTraderTools\bin\Debug\PropTraderTools.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:03.81
```

**SCAN-05: PASS** (0 errors, 0 warnings)

---

### SCAN-06: Test gate

**Command**: `dotnet test tests/PropTraderTools.Tests/PropTraderTools.Tests.csproj`

**Output**:
```
Passed!  - Failed: 0, Passed: 86, Skipped: 3, Total: 89, Duration: 49 ms
```

- Pre-existing passing tests: 76
- New DW-LB-FL-01 tests (T1-T10): 10
- Total passed: 86
- Skipped (pre-existing, unchanged): 3
- Failed: 0

**New tests (T1-T10 all passed)**:
| # | Method | Result |
|---|--------|--------|
| T1 | `HasArmingAtmBrackets_ReturnsFalse_WhenNoOrders` | PASSED |
| T2 | `HasArmingAtmBrackets_ReturnsFalse_WhenAllBracketsAreCancelled` | PASSED |
| T3 | `HasArmingAtmBrackets_ReturnsTrue_WhenStop1IsWorking` | PASSED |
| T4 | `HasArmingAtmBrackets_ReturnsTrue_WhenTarget2IsAccepted` | PASSED |
| T5 | `HasArmingAtmBrackets_ReturnsTrue_WhenStop3IsTriggerPending` | PASSED |
| T6 | `HasArmingAtmBrackets_ReturnsFalse_WhenAtmBracketsForDifferentInstrument` | PASSED |
| T7 | `HasArmingAtmBrackets_ReturnsFalse_WhenOnlyNonAtmOrdersAreWorking` | PASSED |
| T8 | `FlattenIfNotArming_CallsFlattenOne_WhenNoArmingBrackets` | PASSED |
| T9 | `FlattenIfNotArming_SkipsFlattenOne_WhenArmingBracketsPresent` | PASSED |
| T10 | `IsAccountFlattenable_ExistingInflightGuardStillWorks_Regression` | PASSED |

**SCAN-06: PASS** (all 86 tests pass, 10 new T1-T10 all pass)

---

### SCAN-07: Sync gate

**Command**: `powershell -File scripts\ptt-sync-and-verify.ps1`

**Output**:
```
=== PTT SYNC: src/PropTraderTools -> NT8 AddOns ===
  COPIED:  CopyEngine.cs

  Copied:   1  |  In-sync: 17  |  Excluded: 74

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

**SCAN-07: PASS** (0 DESYNC, 0 MISSING, 18 files confirmed)

---

## Build Result

**BUILD_PASS**
- `dotnet build`: 0 errors, 0 warnings
- `dotnet test`: 86 passed, 0 failed
- Sync: 0 DESYNC, 0 MISSING

---

## F5 Gate

**Required**: Press F5 in NinjaTrader 8 (or Tools -> Edit NinjaScript -> Compile).
Sync script confirmed `CopyEngine.cs` copied to NT8 AddOns folder and MD5-verified.
F5 must complete with 0 compile errors before ticket is considered fully DONE.

---

## SIM Gate (manual, post-F5)

**Required**: Clone mode, 3 follower accounts, BE ALL cycle fired, new entry enters.
Confirm: no PTT-Flatten fires on followers during bracket arming window.
(Per ticket acceptance criterion 6 — manual SIM gate.)

---

## Status: BUILD_PASS
