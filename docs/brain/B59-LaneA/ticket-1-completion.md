# B59-LaneA Ticket-1 Completion Report

**Phase**: Ph4a (ptt-engineer)
**Status**: BUILD_PASS
**Commit**: fac65246
**Commit message**: `fix(ptt): B59 -- Gate 0.5 exit-name guard via IsExitSignalName [7 tests]`

---

## Files Modified

| File | Change Type | Lines Affected |
|------|------------|----------------|
| `src/PropTraderTools/CopyEngine.cs` | Insert + Replace | Lines 719-735 (new method), line 744-745 (Gate 0.5 replacement) |
| `src/PropTraderTools/CopyEngineTests.cs` | Insert | Lines 2750-2817 (7 new [Fact] test methods) |

---

## Changes Applied

### B59-T1: `IsExitSignalName` method inserted (CopyEngine.cs, lines 719-735)

New `internal static bool IsExitSignalName(string name)` method inserted after `IsDispatchTriggerState`
(after line 718 in original, now lines 720-733 post-insert). CYC=6. JS-001/JS-002 compliant.

Covers:
1. `null` -> `false` (unknown signal passes through)
2. `PTT-` prefix -> `true` (own signal cascade prevention)
3. `"Close"` -> `true` (NT8 Close button, root cause DW-B59-01)
4. `"Flatten"` -> `true` (NT8 Flatten signal)
5. `"Rev"` -> `true` (NT8 reversal signal)
6. `"Exit"` prefix -> `true` (NT8 "Exit..." family)

### B59-T2 File A: Gate 0.5 guard replaced (CopyEngine.cs, line 744)

**OLD (2 lines)**:
```csharp
// Gate 0.5: PTT-prefix guard -- prevents cascade copy of our own PTT- signals. CYC: 7->8.
if (order.Name != null && order.Name.StartsWith("PTT-")) return;
```

**NEW (2 lines)**:
```csharp
// Gate 0.5: block PTT- cascade AND known NT8 exit signal names (B59). CYC: 7->8 (unchanged).
if (IsExitSignalName(order.Name)) return;
```

CYC unchanged (7->8). The null-check is now encapsulated inside `IsExitSignalName`.

### B59-T2 File B: 7 xUnit tests inserted (CopyEngineTests.cs, lines 2750-2817)

Tests `T_B59_01` through `T_B59_07` added. All test `CopyEngine.IsExitSignalName` directly
(internal static -- no reflection, no NT8 runtime required).

---

## Grep Verification

### A. `IsExitSignalName` in CopyEngine.cs -- must be >= 2 lines

```
Line 720: // B59 T1: IsExitSignalName -- CYC=6. ...
Line 724: internal static bool IsExitSignalName(string name)
Line 745: if (IsExitSignalName(order.Name)) return;
```
**Result: 3 matches -- PASS**

### B. `T_B59_0` in CopyEngineTests.cs -- must be exactly 7 [Fact] test methods

```
Line 2751: // B59 T1: IsExitSignalName -- 7 direct tests (T_B59_01 through T_B59_07)  [comment]
Line 2757: public void T_B59_01_IsExitSignalName_NullName_ReturnsFalse()
Line 2764: public void T_B59_02_IsExitSignalName_PttPrefix_ReturnsTrue()
Line 2773: public void T_B59_03_IsExitSignalName_Close_ReturnsTrue()
Line 2780: public void T_B59_04_IsExitSignalName_Flatten_ReturnsTrue()
Line 2787: public void T_B59_05_IsExitSignalName_Rev_ReturnsTrue()
Line 2794: public void T_B59_06_IsExitSignalName_ExitPrefix_ReturnsTrue()
Line 2803: public void T_B59_07_IsExitSignalName_ArbitrarySignal_ReturnsFalse()
```
**Result: 8 total (7 [Fact] method lines + 1 comment) -- PASS (7 test methods confirmed)**

### C. `order.Name != null` in CopyEngine.cs at Gate 0.5 -- must be 0

```
(no Gate 0.5 match; remaining 4 hits at lines 1487, 1488, 1496, 1514 are unrelated pre-existing logic)
```
**Result: 0 Gate 0.5 instances -- PASS**

---

## 7 Mandatory Scans

| Scan | Pattern | Result | Status |
|------|---------|--------|--------|
| SCAN-01 | `lock(` in src/ | 0 actual lock() calls (comments only reference "no lock") | **PASS** |
| SCAN-02 | Non-ASCII characters | Pre-existing hits at lines 395, 496, 1256, 1257 (CopyEngine.cs) -- none in B59 new code | **PASS** (no new violations) |
| SCAN-03 | `FontFamily` | 0 matches | **PASS** |
| SCAN-04 | `#[0-9A-Fa-f]{6}` | Pre-existing comment annotations in TradeCopierPanel.cs, TradeCopierWindow.cs -- none in B59 new code; color values use `MakeBrush(r,g,b)` correctly | **PASS** (no new violations) |
| SCAN-05 | CreateOrder non-PTT- names | All CreateOrder calls use PTT- prefix ("PTT-BE-Stop", "PTT-Mirror-Close", "PTT-Copy", "PTT-Trim", "PTT-Flatten", "PTT-TrimLimit", "PTT-FlattenLimit") | **PASS** |
| SCAN-06 | `DateTime.Now` (non-UTC) | 0 matches | **PASS** |
| SCAN-07 | `\block\s*\(` (actual lock calls) | 0 actual lock() calls (filtered comments) | **PASS** |

---

## deploy-sync.ps1 Result

`deploy-sync.ps1` does not exist at repository root (archived to `archive/v12-reference/scripts/deploy-sync.ps1`).
Manual sync performed:
- `CopyEngine.cs` copied to `C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\CopyEngine.cs`
- SHA-256 hash verified: both files match (`944F44FE514BBBA1D4B4556D224D65EF29A542965E2906CC1E334BD97B3B7C4C`)

`verify_links.ps1 -Fix` result:
```
OK       : AtrSizingEngine.cs  (copy-only -- run -Fix)
OK       : CopyEngine.cs  (copy-only -- run -Fix)
SKIP     : CopyEngineTests.cs  (test file -- not deployed to NT8)
OK       : TradeCopierAddOn.cs  (hard-linked)
OK       : TradeCopierPanel.cs  (hard-linked)
OK       : TradeCopierWindow.cs  (hard-linked)
SUMMARY: OK=5, DESYNC=0, MISSING=0, FIXED=0, SKIPPED=1
```
**PASS -- All deployable source files match NinjaTrader. No stale deploy risk.**

---

## Deviations from Ticket

**One deviation noted:**

`deploy-sync.ps1` referenced in the ticket does not exist at repository root. Only an archived version exists at `archive/v12-reference/scripts/deploy-sync.ps1`. That archive version maps V12_002 strategy files, not PropTraderTools AddOn files. Manual copy was performed with SHA-256 hash verification. `verify_links.ps1 -Fix` confirmed sync integrity.

All other steps executed exactly as specified. No improvisation.

---

## Commit Details

```
commit fac65246
Author: (current session)
fix(ptt): B59 -- Gate 0.5 exit-name guard via IsExitSignalName [7 tests]
2 files changed, 81 insertions(+), 2 deletions(-)
```
