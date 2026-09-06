# DW-BWAVE-UI-01 — Tickets

**Epic**: DW-BWAVE-UI-01  
**Phase**: 3 — Ticket Generation  
**Status**: TICKETS_COMPLETE  
**Date**: 2026-08-27  
**Author**: ptt-architect  
**Source plan**: `docs/brain/DW-BWAVE-UI-01/02-architecture-plan.md` (REVIEW_PASS)  
**Pipeline**: SINGLE-PIPELINE  
**Ticket count**: 1

---

## T1 — DW-BWAVE-UI-01: Move teal Foreground/BorderThickness assignments after SetResourceReference

### Spec Requirement IDs Satisfied

- **DW-BWAVE-UI-01 (P1)**: BE, BE ALL, Quick, QAll2t buttons show invisible text at rest due to
  `Foreground` and `BorderThickness` being set before `SetResourceReference` triggers style
  application, which overwrites them.

---

### File

```
src/PropTraderTools/TradeCopierPanel.cs
```

---

### Method Signature Referenced

```csharp
// Method to locate and modify (no signature change -- same existing method):
private void BuildBufferedButtonsRow(...)
// Lines affected: 1189-1197 (confirmed by read_file during Phase 1 and Phase 3)
```

> **To engineer**: Do not change the method signature. This ticket is a line reorder within the
> existing method body only.

---

### Problem Statement

In `BuildBufferedButtonsRow`, the current statement order is:

1. `new Button { Content = s.Content }` — button created
2. `if (s.Teal) { btn.BorderBrush = BrushTeal; btn.Foreground = BrushTeal; btn.BorderThickness = new Thickness(2); }` — teal properties set as **local values**
3. `btn.SetResourceReference(Control.StyleProperty, "NTButtonStyle")` — **WPF style applied HERE**
4. `btn.Background = s.Bg` — background set post-style (correct, DW-LaneA-06 fix)

`SetResourceReference` at step 3 triggers full WPF style application. `NTButtonStyle` owns
`Foreground` and `BorderThickness` setters. Per WPF `DependencyProperty` precedence, a local
value set *before* `SetResourceReference` fires is overwritten by the style setter; a local value
set *after* wins. Because `btn.Foreground = BrushTeal` (line 1193) and
`btn.BorderThickness = new Thickness(2)` (line 1194) are assigned before `SetResourceReference`
(line 1196), the style overwrites them on every button construction cycle.

**Result**: teal button text is invisible at rest. Text becomes visible on hover only because the
`NTButtonStyle` trigger swaps the `Foreground` value when `IsMouseOver` is true.

**Precedent**: `btn.Background = s.Bg` at line 1197 already sits post-`SetResourceReference`
following the DW-LaneA-06 fix. That fix proves the post-style pattern is correct and that
`NTButtonStyle` does not override an explicitly set `Background` when the assignment is made
after the `SetResourceReference` call.

---

### Fix Design

**BEFORE (current — buggy, lines 1189-1197 confirmed by read_file):**

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

**AFTER (fixed — move SetResourceReference to before the if block):**

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

> **NOTE TO ENGINEER**: The architect approved moving `SetResourceReference` to BEFORE the
> `if (s.Teal)` block. This ensures all three teal property assignments (`BorderBrush`,
> `Foreground`, `BorderThickness`) are post-style, which is equivalent to and consistent with
> the DW-LaneA-06 pattern already applied to `Background`. `btn.Background = s.Bg` remains
> AFTER the `if` block — this is unchanged and must not be disturbed.

**Mechanical change**: Cut `btn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");`
from its position after line 1195 (`}`) and paste it immediately after line 1189
(`var btn = new Button { Content = s.Content };`). No other edits.

**Net diff**: 0 lines added, 0 lines deleted — pure reorder.

---

### CYC Impact

**Delta: 0**

The `if (s.Teal)` branch existed before this fix and is unchanged. No new conditional branches,
loops, switch expressions, or early returns are introduced. `BuildBufferedButtonsRow` CCN is
unchanged.

---

### JS Rule Constraints Checked

| Rule ID | Rule | Constraint Applied |
|---|---|---|
| JS-021 | `lock()` ban | No `lock()` introduced or present in TradeCopierPanel.cs |
| JS-001 | No `throw` in hot path | No `throw` introduced |
| JS-002 | No `return null` | No `return null` introduced |
| JS-033 | No `async void` (non-event-handler) | No `async` methods added |
| JS-036 | No heap alloc in hot path | `new Thickness(2)` is a value-type struct (stack-allocated) — not a heap allocation |
| JS-037 | No `new T[]` without ArrayPool in hot path | No arrays introduced |
| ASCII-only | All identifiers and strings ASCII | No Unicode, emoji, or non-ASCII characters introduced |
| No DateTime.Now | Use `DateTime.UtcNow` | Not applicable — no date/time usage |
| No FontFamily | FontFamily ban | Not applicable |
| No hex color literals | Use named brush constants | `BrushTeal` is an existing project constant — no hex literals |
| CYC <= 8 | Cyclomatic complexity limit | Delta = 0; existing CCN unchanged |

---

### xUnit Tests

**None required.**

This is a pure line reorder. No new logic, no new methods, no new branches, no new state
transitions are introduced. There is no testable unit of logic to add. Correctness is verified
structurally via SCAN-1 and visually via the Director-owned SIM gate.

---

### 7-Scan Checklist (Engineer Contract — Layer 1)

All 7 scans MUST reach zero before `BUILD_PASS` is declared. Run in order. Do not skip.

---

**SCAN-1 — Post-style Foreground placement (structural correctness)**

```powershell
grep -n "Foreground\|SetResourceReference" src/PropTraderTools/TradeCopierPanel.cs `
  | grep -A2 -B2 "BrushTeal\|NTButtonStyle"
```

Expected: The `SetResourceReference` line number appears **before** the `Foreground = BrushTeal`
line number in the output for the `BuildBufferedButtonsRow` block.

PASS condition: `SetResourceReference` line N < `btn.Foreground = BrushTeal` line N+k (k > 0).

---

**SCAN-2 — CCN gate (complexity unchanged)**

```powershell
lizard src/PropTraderTools/TradeCopierPanel.cs --CCN 8
```

Expected: 0 warnings emitted for methods introduced or modified by this ticket.

PASS condition: Zero new CCN > 8 warnings. Any warning on a method touched by this ticket is a
blocker.

---

**SCAN-3 — lock() forensic (JS-021)**

```powershell
grep -n "lock\s*(" src/PropTraderTools/TradeCopierPanel.cs
```

Expected: 0 results.

PASS condition: Zero matches. Any match is a P0 blocker per JS-021.

---

**SCAN-4 — ASCII-only check**

```powershell
([System.IO.File]::ReadAllBytes("src/PropTraderTools/TradeCopierPanel.cs") `
  | Where-Object { $_ -gt 127 } | Measure-Object).Count
```

Expected: Count = 0.

PASS condition: Zero bytes with value > 127.

---

**SCAN-5 — Build gate**

```powershell
dotnet build src/PropTraderTools/PropTraderTools.csproj
```

Expected: 0 errors, 0 warnings.

PASS condition: Clean build. Any error or new warning is a blocker.

---

**SCAN-6 — NT8 forbidden patterns**

```powershell
grep -n "Account\.Change\|AtmStrategyCreate\|AtmStrategyChangeStopTarget" `
  src/PropTraderTools/TradeCopierPanel.cs
```

Expected: 0 results.

PASS condition: Zero matches. These NT8 APIs are `StrategyBase`-only and forbidden in `AddOnBase`
scope per NT8 API mandate.

---

**SCAN-7 — async void gate (JS-033)**

```powershell
grep -n "async void " src/PropTraderTools/TradeCopierPanel.cs
```

Expected: 0 new `async void` introductions. Pre-existing event-handler `async void` entries
(if any) are acceptable; this ticket must not add any new ones.

PASS condition: Count of `async void` occurrences after fix equals count before fix.

---

### Acceptance Criteria

1. `btn.Foreground = BrushTeal` line number is **greater than** `btn.SetResourceReference(Control.StyleProperty, "NTButtonStyle")` line number in `BuildBufferedButtonsRow` (SCAN-1 pass).
2. All 7 scans pass at zero.
3. `dotnet build` produces 0 errors and 0 warnings (SCAN-5 pass).
4. `btn.Background = s.Bg` line remains after the `if (s.Teal)` block — DW-LaneA-06 fix is not regressed.

---

### SIM Gate (Director-owned, after F5 recompile)

The following must be visually confirmed in NinjaTrader 8 with the fix applied and F5 recompile
completed:

| Button | Acceptance |
|---|---|
| BE | Teal text **visible at rest** (not hover-only). Teal border visible at rest. |
| BE ALL | Teal text **visible at rest** (not hover-only). Teal border visible at rest. |
| Quick | Teal text **visible at rest** (not hover-only). Teal border visible at rest. |
| QAll2t | Teal text **visible at rest** (not hover-only). Teal border visible at rest. |
| All four buttons | Background color (`s.Bg`) retained — DW-LaneA-06 fix not regressed. |

---

### Sync Step (after BUILD_PASS)

```powershell
powershell -File scripts\ptt-sync-and-verify.ps1
```

Expected: 0 MISMATCH lines. Then press F5 in NinjaTrader 8 to recompile.

---

**T1 END**

---

## Open Deferred Items (not closed by this epic)

| Item | Status | Note |
|---|---|---|
| DW-C39-09-TEST (xUnit for OnAddRule) | OPEN, P2 | Not affected. Remains open. |
| PRE-EXISTING-COPYENGINE-CCN (33 methods CCN > 8) | OPEN, P2 | Not affected. Remains open. |

---

**TICKETS_COMPLETE**
