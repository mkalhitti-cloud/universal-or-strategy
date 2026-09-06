# DW-BWAVE-UI-01 — Architecture Plan

**Epic**: DW-BWAVE-UI-01  
**Phase**: 1 — Architecture Plan  
**Status**: REVIEW_PASS  
**Date**: 2026-08-27  
**Author**: ptt-architect  

---

## 1. Defect Description and Root Cause

### Defect

**DW-BWAVE-UI-01 (P1)**: BE, BE ALL, Quick, and QAll2t buttons show a solid teal background
with invisible text at rest. Text becomes visible only on hover.

### Root Cause (confirmed — do not re-investigate)

File: [`src/PropTraderTools/TradeCopierPanel.cs`](../../src/PropTraderTools/TradeCopierPanel.cs)  
Method: `BuildBufferedButtonsRow`  
Lines: 1190–1197 (confirmed by `read_file` during Phase 1 planning)

The exact current state at lines 1189–1197:

```csharp
var btn = new Button { Content = s.Content };
if (s.Teal)
{
    btn.BorderBrush = BrushTeal;              // line 1192 — BEFORE SetResourceReference
    btn.Foreground = BrushTeal;               // line 1193 — PROBLEM: overwritten by style
    btn.BorderThickness = new Thickness(2);   // line 1194 — overwritten by style
}
btn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");  // line 1196
btn.Background = s.Bg; // AFTER style -- explicit brush wins (DW-LaneA-06 fix)  // line 1197
```

**Mechanism**: `btn.SetResourceReference(Control.StyleProperty, "NTButtonStyle")` at line 1196
triggers full WPF style application. `NTButtonStyle` contains its own `Foreground` setter and
`BorderThickness` setter. When a `SetResourceReference` call fires, the DependencyObject
internal value store is updated with a `DynamicResourceExpression` for the `StyleProperty`,
which triggers style application and overwrites local property assignments made *before* the call.
Properties set *after* `SetResourceReference` are written as new local values that win over
style-setter values per WPF DependencyProperty precedence.

**Precedent**: `btn.Background = s.Bg` at line 1197 already sits after `SetResourceReference`
following the DW-LaneA-06 fix. That fix proved the post-style pattern is correct. `Foreground`
and `BorderThickness` must follow the same pattern.

---

## 2. Lane-Split Gate

| Question | Answer |
|---|---|
| Q1. Same method or within 50 lines? | YES — single method `BuildBufferedButtonsRow`, lines 1190–1197, span of 8 lines |
| Q2. Fix B design depends on Fix A? | N/A — single fix |
| Q3. Each fix has standalone value if other is blocked? | N/A — single fix |
| Q4. Each fix has independent SIM verification path? | N/A — single fix |

**LANE-SPLIT GATE RESULT: SINGLE-PIPELINE**

One fix. One ticket. One pipeline.

---

## 3. Fix Design

### Before (current — buggy)

```csharp
var btn = new Button { Content = s.Content };
if (s.Teal)
{
    btn.BorderBrush = BrushTeal;              // line 1192
    btn.Foreground = BrushTeal;               // line 1193  <-- set BEFORE style → overwritten
    btn.BorderThickness = new Thickness(2);   // line 1194  <-- set BEFORE style → overwritten
}
btn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");  // line 1196
btn.Background = s.Bg; // AFTER style -- explicit brush wins (DW-LaneA-06 fix)  // line 1197
```

### After (fixed)

```csharp
var btn = new Button { Content = s.Content };
btn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");  // moved BEFORE if block
if (s.Teal)
{
    btn.BorderBrush = BrushTeal;              // now AFTER SetResourceReference → wins
    btn.Foreground = BrushTeal;               // now AFTER SetResourceReference → wins (FIX)
    btn.BorderThickness = new Thickness(2);   // now AFTER SetResourceReference → wins (FIX)
}
btn.Background = s.Bg; // AFTER style -- explicit brush wins (DW-LaneA-06 fix intact)
```

### Change Description

Move `btn.SetResourceReference(Control.StyleProperty, "NTButtonStyle")` from its current
position (after the `if (s.Teal)` block) to immediately *before* the `if (s.Teal)` block.
This places all three teal property assignments (`BorderBrush`, `Foreground`, `BorderThickness`)
after style application, matching the pattern already used for `btn.Background` (DW-LaneA-06).

**Lines changed**: 1 line relocated (SetResourceReference). No lines deleted. No lines added.
Net diff: +0 lines (pure reorder within the same method body).

---

## 4. Scope

| Dimension | Value |
|---|---|
| File | `src/PropTraderTools/TradeCopierPanel.cs` |
| Method | `BuildBufferedButtonsRow` |
| Lines affected | ~1189–1197 (confirmed from `read_file`) |
| New files | None |
| New methods | None |
| New classes | None |
| New branches | None |
| NT8 AddOn APIs changed | None |
| Threading model changed | No |

---

## 5. CYC Impact

**CYC delta: 0**

The `if (s.Teal)` branch already exists before this fix and is preserved unchanged. No new
conditional branches, no new loops, no new switch expressions, no early returns added.
The reorder moves `SetResourceReference` one position earlier in the statement sequence — a
pure control-flow-neutral reorder.

Lizard CCN for `BuildBufferedButtonsRow` is unchanged by this fix.

---

## 6. Scan Requirements

All 5 scans must pass before this ticket is considered VERIFY_PASS.

### SCAN-1 — Post-style Foreground placement

```powershell
# Verify btn.Foreground = BrushTeal appears AFTER SetResourceReference in file.
# Approach: confirm the line number of Foreground assignment > line number of SetResourceReference
# within the BuildBufferedButtonsRow block.
grep -n "SetResourceReference" src/PropTraderTools/TradeCopierPanel.cs
grep -n "Foreground = BrushTeal" src/PropTraderTools/TradeCopierPanel.cs
# PASS: Foreground line number > SetResourceReference line number in the buffered button block.
```

**Expected**: `SetResourceReference` line N, `Foreground = BrushTeal` line N+k where k > 0.

### SCAN-2 — CCN gate

```powershell
lizard src/PropTraderTools/TradeCopierPanel.cs --CCN 8
# PASS: 0 warnings emitted for TradeCopierPanel.cs (no method exceeds CCN 8 as result of this fix)
```

**Expected**: Zero new CCN > 8 warnings introduced.

### SCAN-3 — lock() forensic

```powershell
grep -n "lock\s*(" src/PropTraderTools/TradeCopierPanel.cs
# PASS: 0 matches (no lock() in TradeCopierPanel.cs)
```

**Expected**: Zero matches. Any match is a P0 blocker per JS-021.

### SCAN-4 — ASCII-only

```powershell
$bytes = [System.IO.File]::ReadAllBytes("src/PropTraderTools/TradeCopierPanel.cs")
($bytes | Where-Object { $_ -gt 127 }).Count
# PASS: 0
```

**Expected**: Zero bytes > 127.

### SCAN-5 — Build gate

```powershell
dotnet build src/PropTraderTools/PropTraderTools.csproj
# PASS: 0 errors, 0 new warnings
```

**Expected**: Clean build. Zero errors.

---

## 7. xUnit Test Note

**No test ticket required.**

This fix is a line reorder — no new logic, no new methods, no new branches, no new state
transitions. There is no testable unit of logic introduced. The acceptance criterion is
verified visually via SIM gate (see §8) and structurally via SCAN-1.

Carrying forward: `DW-C39-09-TEST` (OPEN, P2) from LaneA deferred backlog remains open.
`PRE-EXISTING-COPYENGINE-CCN` (OPEN, P2) from LaneA deferred backlog remains open.
Neither is closed by this epic.

---

## 8. Acceptance Criteria — SIM Gate

The following must be observed in NinjaTrader 8 after F5 recompile with the fix applied:

1. **BE button**: teal text visible at rest, without hovering. Teal border visible at rest.
2. **BE ALL button**: teal text visible at rest, without hovering. Teal border visible at rest.
3. **Quick button**: teal text visible at rest, without hovering. Teal border visible at rest.
4. **QAll2t button**: teal text visible at rest, without hovering. Teal border visible at rest.
5. **Hover behavior unchanged**: text remains visible (or changes per NTButtonStyle trigger) on hover.
6. **Background unchanged**: buttons retain their assigned background color (`s.Bg`), confirming the DW-LaneA-06 fix is not regressed.
7. **Build gate**: `dotnet build` 0 errors before F5.
8. **SCAN-1 through SCAN-5**: all pass (see §6).

---

## 9. Prior Backlog Impact

| Item | Status | Impact |
|---|---|---|
| DW-C39-09-TEST (xUnit for OnAddRule) | OPEN, P2 | Not affected. Remains open. |
| PRE-EXISTING-COPYENGINE-CCN (33 methods) | OPEN, P2 | Not affected. Remains open. |

---

## 10. Rules Catalog Gate Result

**RULES CATALOG GATE: PASS**

| Rule | Check | Result |
|---|---|---|
| JS-021 (`lock()` ban) | No lock() introduced | PASS |
| JS-001 (throw in hot path) | No new throw statements | PASS |
| JS-002 (return null) | No new null returns | PASS |
| JS-033 (async void) | No async methods added | PASS |
| JS-036/037 (heap alloc) | `new Thickness(2)` is a value-type (struct), stack-allocated | PASS |
| ASCII-only | All identifiers ASCII | PASS |
| No DateTime.Now | None introduced | PASS |
| No FontFamily | None introduced | PASS |
| No hex color literals | BrushTeal is existing project constant | PASS |
| CYC <= 8 | Delta = 0 | PASS |

---

## 11. Component Summary

| Component | Description |
|---|---|
| **File** | `src/PropTraderTools/TradeCopierPanel.cs` |
| **Method** | `BuildBufferedButtonsRow` |
| **Fix type** | Line reorder — move `SetResourceReference` before `if (s.Teal)` block |
| **WPF mechanism** | Post-style local value assignment wins over style setter per DependencyProperty precedence |
| **Precedent** | DW-LaneA-06 (`btn.Background = s.Bg` at line 1197 already uses this pattern) |
| **NT8 API** | No NT8 AddOn API involved. Pure WPF UI construction. |
| **Pipeline** | SINGLE-PIPELINE |
| **Tickets** | 1 (T1 only) |

---

**PLAN_COMPLETE**
