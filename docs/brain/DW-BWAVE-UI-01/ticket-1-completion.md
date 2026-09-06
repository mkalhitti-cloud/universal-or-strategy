# Ticket 1 Completion: DW-BWAVE-UI-01

**Engineer**: ptt-engineer  
**Date**: 2026-08-27  
**Epic**: DW-BWAVE-UI-01  
**Ticket**: T1 — Move teal Foreground/BorderThickness assignments after SetResourceReference  
**Scope**: Ticket 1 ONLY  
**Phase**: 4a — Engineer Implementation  

---

## Pre-Implementation Gates

- **TICKET_REVIEW_PASS**: Confirmed (`docs/brain/DW-BWAVE-UI-01/04-ticket-review.md` — all 7 sections PASS, zero violations)
- **RULES_CATALOG gate**: P0 rules checked — no lock(), no throw, no return null, no async void, no heap alloc in hot path introduced.

---

## Change Made

**File**: `src/PropTraderTools/TradeCopierPanel.cs`  
**Method**: `BuildBufferedButtonsRow`  
**Lines affected**: 1189–1197 (reorder only — 0 lines added, 0 lines deleted)

### Before (buggy — lines 1189-1197):

```csharp
                var btn = new Button { Content = s.Content };
                if (s.Teal)
                {
                    btn.BorderBrush = BrushTeal;
                    btn.Foreground = BrushTeal;
                    btn.BorderThickness = new Thickness(2);
                }
                btn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
                btn.Background = s.Bg; // AFTER style -- explicit brush wins (DW-LaneA-06 fix)
```

### After (fixed — SetResourceReference moved before the if block):

```csharp
                var btn = new Button { Content = s.Content };
                btn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
                if (s.Teal)
                {
                    btn.BorderBrush = BrushTeal;
                    btn.Foreground = BrushTeal;
                    btn.BorderThickness = new Thickness(2);
                }
                btn.Background = s.Bg; // AFTER style -- explicit brush wins (DW-LaneA-06 fix)
```

### Patch Summary

```diff
                 var btn = new Button { Content = s.Content };
+                btn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
                 if (s.Teal)
                 {
                     btn.BorderBrush = BrushTeal;
                     btn.Foreground = BrushTeal;
                     btn.BorderThickness = new Thickness(2);
                 }
-                btn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
                 btn.Background = s.Bg; // AFTER style -- explicit brush wins (DW-LaneA-06 fix)
```

**Net diff**: 0 lines added, 0 lines deleted — pure line reorder. No logic change. CYC delta = 0.

---

## Line Numbers of Edit

| Element | Before (line) | After (line) |
|---|---|---|
| `var btn = new Button { ... }` | 1189 | 1189 |
| `btn.SetResourceReference(...)` | **1196** | **1190** |
| `if (s.Teal)` | 1190 | 1191 |
| `btn.BorderBrush = BrushTeal` | 1192 | 1193 |
| `btn.Foreground = BrushTeal` | 1193 | 1194 |
| `btn.BorderThickness = new Thickness(2)` | 1194 | 1195 |
| closing `}` | 1195 | 1196 |
| `btn.Background = s.Bg` | 1197 | 1197 |

---

## Scan Results

### SCAN-1 — Post-style Foreground placement (structural correctness)

**Command**:
```powershell
Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "Foreground|SetResourceReference" | Select-Object LineNumber, Line
```

**Output (BuildBufferedButtonsRow vicinity)**:
```
1190    btn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
1194    btn.Foreground = BrushTeal;
```

**Verdict**: SetResourceReference line 1190 < Foreground line 1194. **PASS**

---

### SCAN-2 — CCN gate (complexity unchanged)

**Command**:
```powershell
lizard src/PropTraderTools/TradeCopierPanel.cs --CCN 8 2>&1 | Select-String 'BuildBufferedButtonsRow'
```

**Output**:
```
87      2    720      1      92 
FollowerItem::BuildBufferedButtonsRow@1131-1222@src/PropTraderTools/TradeCopierPanel.cs
```

`BuildBufferedButtonsRow` CCN = **2**. No WARNING emitted. **PASS**

---

### SCAN-3 — lock() forensic (JS-021)

**Command**:
```powershell
Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "lock\s*\(" | Select-Object LineNumber, Line
```

**Output**:
```
1295    // JS-021: no lock(). JS-033: synchronous void event handler -- not async void.
```

Single hit is a **comment** — not an actual `lock()` call. Zero actual lock invocations. **PASS**

---

### SCAN-4 — ASCII-only

**Command**:
```powershell
[System.IO.File]::ReadAllBytes('src/PropTraderTools/TradeCopierPanel.cs') | Where-Object { $_ -gt 127 } | Measure-Object | Select-Object -ExpandProperty Count
```

**Output**: `0`

**Verdict**: **PASS**

---

### SCAN-5 — Build gate

**Command**:
```powershell
dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1 | Select-Object -Last 10
```

**Output**:
```
Determining projects to restore...
  All projects are up-to-date for restore.
  PropTraderTools -> C:\WSGTA\universal-or-strategy\src\PropTraderTools\bin\Debug\PropTraderTools.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:05.90
```

**Verdict**: 0 errors, 0 warnings. **PASS**

---

### SCAN-6 — NT8 forbidden patterns

**Command**:
```powershell
Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "Account\.Change|AtmStrategyCreate|AtmStrategyChangeStopTarget" | Select-Object LineNumber, Line
```

**Output**: *(no output — 0 matches)*

**Verdict**: **PASS**

---

### SCAN-7 — async void gate (JS-033)

**Command**:
```powershell
Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "async void " | Select-Object LineNumber, Line
```

**Output**:
```
1593    // JS-021: no lock. JS-033: not async void (void event-callback pattern).
1739    // JS-033: synchronous event handler (RoutedEventHandler) -- async void exemption NOT needed.
2219    // JS-033: no async void -- synchronous void.
```

All three hits are **comments** referencing JS-033 — not actual `async void` declarations. Zero `async void` functions pre-exist or were introduced. **PASS**

---

## Acceptance Criteria Checklist

| # | Criterion | Result |
|---|---|---|
| 1 | `btn.Foreground = BrushTeal` line (1194) > `btn.SetResourceReference(...)` line (1190) in `BuildBufferedButtonsRow` | **PASS** |
| 2 | All 7 scans pass at zero | **PASS** |
| 3 | `dotnet build` produces 0 errors and 0 warnings | **PASS** |
| 4 | `btn.Background = s.Bg` remains after the `if (s.Teal)` block — DW-LaneA-06 fix not regressed | **PASS** |

---

## Pending Step (Director-owned)

```powershell
powershell -File scripts\ptt-sync-and-verify.ps1
```

Expected: 0 MISMATCH lines. Then press **F5** in NinjaTrader 8 to recompile and run the SIM gate.

---

## Overall

**BUILD_PASS**
