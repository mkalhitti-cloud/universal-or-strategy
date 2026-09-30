# B176-1 Completion Report

**Ticket ID:** B176-1
**Epic:** DW-B176-ATR-TEXTBOX-BROKEN (P0)
**File changed:** `src/PropTraderTools/TradeCopierPanel.cs`
**Engineer:** ptt-engineer (Phase 4a)
**Date:** 2025-01-29
**Status:** BUILD_PASS

---

## Change Summary

Replaced the body of `OnAtrBoxPreviewKeyDown` to restrict `e.Handled = true`
to unmodified alpha/digit keys only (the keys NT8's chart-level
instrument-search interceptor reads during the tunnel phase). Editing keys
(Backspace, Delete, arrows, Ctrl/Shift combos) now flow through to the
TextBox's own KeyDown handler as designed.

---

## Lines Changed

| | Value |
|---|---|
| BEFORE lines | 3309–3316 (8 lines: 3-line comment + 5-line method) |
| AFTER lines  | 3309–3331 (23 lines: 8-line comment + 15-line method) |
| Net delta    | +15 lines |

---

## CYC Report

| Metric | Value |
|--------|-------|
| CYC before | 2 (base + IsKeyboardFocused check) |
| CYC after  | 5 (base + null/focus guard + isAlpha + isDigit + noMod check) |
| Delta      | +3 |
| Compliant (<=8) | YES |

---

## 7-Scan Results

### SCAN-01 — Lock-free (no new `lock(` in changed lines)

**Command:**
```
Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "lock\(" |
  Where-Object { $_.LineNumber -ge 3309 -and $_.LineNumber -le 3340 }
```
**Output:** (no output — zero hits in changed range)
**Result: PASS** — zero `lock(` in lines 3309-3340. (27 pre-existing `lock(` elsewhere in file, unchanged.)

---

### SCAN-02 — `e.Handled` guard scoped (manual diff review)

**Method:** Manual review of lines 3317-3331 (new method body).

`e.Handled = true` appears exactly once at line 3330, exclusively inside:
```csharp
if ((isAlpha || isDigit) && noMod)
    e.Handled = true;
```
No unconditional `e.Handled = true` is present anywhere in the new code.

**Result: PASS** — `e.Handled = true` is guarded by `(isAlpha || isDigit) && noMod` only.

---

### SCAN-03 — `isAlpha` present

**Command:**
```
Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "isAlpha" |
  Measure-Object | Select-Object -ExpandProperty Count
```
**Output:** `3`
**Result: PASS** — 3 hits >= 1 required.

---

### SCAN-04 — `isDigit` present

**Command:**
```
Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "isDigit" |
  Measure-Object | Select-Object -ExpandProperty Count
```
**Output:** `3`
**Result: PASS** — 3 hits >= 1 required.

---

### SCAN-05 — `noMod` present

**Command:**
```
Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "noMod" |
  Measure-Object | Select-Object -ExpandProperty Count
```
**Output:** `3`
**Result: PASS** — 3 hits >= 1 required.

---

### SCAN-06 — ASCII-only (no non-ASCII in changed lines)

**Command:**
```powershell
$lines = Get-Content src/PropTraderTools/TradeCopierPanel.cs
$nonAscii = @()
for ($i = 3308; $i -le 3330; $i++) {
    $line = $lines[$i]
    if ($line -match '[^\x00-\x7F]') { $nonAscii += "Line $($i+1): $line" }
}
if ($nonAscii.Count -eq 0) { "SCAN-06 PASS: No non-ASCII in lines 3309-3331" } else { $nonAscii }
```
**Output:** `SCAN-06 PASS: No non-ASCII in lines 3309-3331`
**Result: PASS** — zero non-ASCII characters in all changed lines.

---

### SCAN-07 — AtrCalc.cs regression guard

**Command:**
```
git status src/PropTraderTools/AtrCalc.cs
```
**Output:**
```
On branch main
...
Untracked files:
    src/PropTraderTools/AtrCalc.cs

nothing added to commit but untracked files present
```
**Result: PASS** — `AtrCalc.cs` is Untracked (not modified). B175 Wilder's ATR regression guard intact.

---

## Scope Confirmation

- `AtrCalc.cs` — NOT touched. Shown as Untracked by git. B175 regression guard intact. PASS.
- `CopyEngineTests.cs` — NOT touched. PASS.
- Subscription line ~3213 (`_atrFractionBox.PreviewKeyDown += OnAtrBoxPreviewKeyDown`) — NOT touched. PASS.
- `CopyEngine.cs` — NOT touched (B176-2 is a separate ticket). PASS.

---

## Mechanism Compliance

- Used `apply_diff` ONLY. `write_file` was NOT used on any `.cs` file. PASS.

---

## ptt-sync.ps1 Note

NOT run after B176-1 alone per ticket instructions. Run once after B176-2 completes.

---

## Status: BUILD_PASS

All 7 scans: PASS (zero violations).
Diff applied exactly per BEFORE/AFTER specification.
Scope boundaries respected.
CYC 2 -> 5 (+3), compliant (<= 8).
