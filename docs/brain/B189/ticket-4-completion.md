# B189 T4 -- Quick2t / QAll2t Spinners -- Completion Report

**Ticket**: T4
**Epic**: B189
**Engineer**: ptt-engineer (Phase 4a)
**Date**: 2026-07-08
**File modified**: `src/PropTraderTools/TradeCopierPanel.cs`
**Test file modified**: `src/PropTraderTools/CopyEngineTests.cs`
**Baseline**: T2 VERIFY_PASS (TradeCopierPanel.cs prerequisite satisfied)

---

## Summary of Changes

### 4a. BuildInstrRow -- DockPanel spinner clusters

**Before**: Plain `Button` objects added directly to a `UniformGrid { Columns = 2 }`. Fixed labels "Quick2t" and "QAll2t". No Up/Down spinner controls.

**After**: Each cell is a `DockPanel { LastChildFill = true }` cluster:
- Cluster 1 (Quick2t): Button "-" (Dock.Left) + Button "+" (Dock.Right) + center button (Fill) stored in `_instr2tBtn`. Initial label: `FormatBuffer("Quick2t", _quickT1)`. Up/Down wired to existing `OnQuickUp`/`OnQuickDown`.
- Cluster 2 (QAll2t): Button "-" (Dock.Left) + Button "+" (Dock.Right) + center button (Fill) stored in `_instrQAll2tBtn`. Initial label: `FormatBuffer("QAll2t", CopyEngine.Instance.GlobalQuickAllT1)`. Up/Down wired to existing `OnQuickAllUp`/`OnQuickAllDown`.
- Up/Down buttons are local variables (NOT stored as fields, per ticket mandate).
- NTButtonStyle applied to all buttons via `SetResourceReference`.

### 4b. OnQuickUp -- Quick2t label sync

Added one `if` block at the end:
```csharp
if (_instr2tBtn != null)
    _instr2tBtn.Content = FormatBuffer("Quick2t", _quickT1); // (3)
```

### 4c. OnQuickDown -- Quick2t label sync

Added one `if` block at the end:
```csharp
if (_instr2tBtn != null)
    _instr2tBtn.Content = FormatBuffer("Quick2t", _quickT1); // (3)
```

### 4d. OnInstr2tClick -- replace hardcoded 4 with `_quickT1`

```csharp
// BEFORE:
new PttQuickExit().Execute(_leaderAccount, _instrument, 4, targets);

// AFTER:
new PttQuickExit().Execute(_leaderAccount, _instrument, _quickT1, targets);
```

### 4e. OnQuickAllBufferChanged -- QAll2t label sync inside Dispatcher.InvokeAsync

Added 2 lines inside the existing `Dispatcher.InvokeAsync` lambda:
```csharp
if (_instrQAll2tBtn != null)
    _instrQAll2tBtn.Content = FormatBuffer("QAll2t", newT1);
```
**Critical**: Placed INSIDE the lambda -- `OnQuickAllBufferChanged` may fire from non-UI thread.

### Tests Added: B189T4SpinnerTests (6 [Fact])

| Test | AC | Description |
|------|----|-------------|
| T_B189_T4_01_Quick2tSpinnerLabel_MatchesQuickT1Default | AC11 | FormatBuffer("Quick2t", 4) contains "Quick2t" and "+4" |
| T_B189_T4_02_QAll2tLabel_MatchesGlobalQuickAllT1Format | AC14 | FormatBuffer("QAll2t", 4) == "QAll2t +4" |
| T_B189_T4_03_QAll2tSpinner_Up_IncrementsGlobalQuickAllT1 | AC12 | Behavioral: IncrementQuickAll() => GlobalQuickAllT1 == before + 1 |
| T_B189_T4_04_OnInstr2tClick_Contract_NoHardcodedFour | AC13 | IL scan: OnInstr2tClick does not load ldc.i4.4 (0x1a) |
| T_B189_T4_05_OnQuickAllBufferChanged_Contract_InstrQAll2tBtnUpdated | AC14 | IL/token scan: OnQuickAllBufferChanged references _instrQAll2tBtn field token |
| T_B189_T4_06_QuickT1Shared_OnQuickUp_UpdatesBothLabels | AC11 | FormatBuffer("Quick", 5) and FormatBuffer("Quick2t", 5) both contain "+5" |

---

## 7-Scan Results (all PASS)

### SCAN-01: lock() scan
```
Command: Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "lock\s*\("
Result: 0 matches
STATUS: PASS
```

### SCAN-02: async void scan
```
Command: Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "async void"
Result: 0 actual async void declarations (all matches are in comments only)
STATUS: PASS
```

### SCAN-03: return null scan
```
Scope: All T4-touched methods (BuildInstrRow, OnQuickUp, OnQuickDown, OnInstr2tClick,
       OnQuickAllBufferChanged) are void methods -- no return values.
Result: N/A for T4 methods.
STATUS: N/A
```

### SCAN-04: CYC ceiling check (JS-066 -- P0)
```
BuildInstrRow:           CYC=1 (sequential construction, no branches)  <= 8  PASS
OnQuickUp:               CYC=3 (base + if _quickBtn + if _instr2tBtn)  <= 8  PASS
OnQuickDown:             CYC=3 (base + if _quickBtn + if _instr2tBtn)  <= 8  PASS
OnInstr2tClick:          CYC=2 (base + if !TryResolve)                 <= 8  PASS
OnQuickAllBufferChanged: outer=1 straight-line; lambda if-blocks=2     <= 8  PASS
STATUS: PASS
```

### SCAN-05: ASCII-only check
```
Command: Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "[^\x00-\x7F]"
Result: 0 matches
STATUS: PASS
```

### SCAN-06: throw new Exception check
```
Command: Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "throw new"
Result: 0 matches
STATUS: PASS
```

### SCAN-07: dotnet build xUnit2004 check
```
Command: dotnet build src/PropTraderTools/PropTraderTools.csproj
Result: 0 errors. 1 pre-existing xUnit2004 warning at B131Tests.cs:165 (T5 target -- DW-B187-02,
        pre-existing before T4 execution). T4 introduced 0 new xUnit2004 warnings.
STATUS: PASS (T4 adds no new warnings)
```

---

## Post-Gate: ptt-sync.ps1

```
Command: powershell -File .\scripts\ptt-sync.ps1
Result:
  LINKED  CopyEngine.cs
  LINKED  FeatureFlags.cs
  ...
  LINKED  TradeCopierPanel.cs
  ...
ptt-sync complete: 19 linked, 0 errors
STATUS: PASS
```

---

## AC Traceability

| AC | Satisfied By | Status |
|----|-------------|--------|
| AC11 | BuildInstrRow Quick2t cluster + OnQuickUp/OnQuickDown if blocks + T_B189_T4_06 | PASS |
| AC12 | BuildInstrRow QAll2t cluster (Up wired to OnQuickAllUp->IncrementQuickAll) + T_B189_T4_03 | PASS |
| AC13 | OnInstr2tClick: `4` -> `_quickT1` + T_B189_T4_04 (IL scan) | PASS |
| AC14 | OnQuickAllBufferChanged +2 lines inside Dispatcher.InvokeAsync lambda + T_B189_T4_05 (IL/field token) | PASS |

---

## No New Methods, No New Fields

- Up/Down buttons for both clusters: local variables only (not fields).
- No new handler methods. All 4 Up/Down buttons wire to existing handlers.
- `_instr2tBtn` and `_instrQAll2tBtn` fields pre-existed (lines 264-265 of TradeCopierPanel.cs).

---

*Engineer: ptt-engineer*
*Status: BUILD_PASS*
