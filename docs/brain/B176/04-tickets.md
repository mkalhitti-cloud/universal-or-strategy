# B176 Tickets — ATR TextBox PreviewKeyDown Fix + Comment Correction

**Status:** TICKETS_COMPLETE (CYCLE 2 — violations #1/#2/#3 fixed)
**Epic:** DW-B176-ATR-TEXTBOX-BROKEN (B176-1) / DW-B159-K5 (B176-2)
**Plan status at time of ticket generation:** REVIEW_PASS

---

## SCOPE LOCK -- TICKET B176-1 ONLY
Do NOT implement or reference any other ticket in this session.

---

## Ticket B176-1

**Ticket ID:** B176-1
**File:** `src/PropTraderTools/TradeCopierPanel.cs`
**Mechanism:** `apply_diff` ONLY. Do NOT use `write_file` on any `.cs` file.
**Epic ID:** DW-B176-ATR-TEXTBOX-BROKEN (P0)
**Spec requirement IDs:** DW-B176-ATR-TEXTBOX-BROKEN

### Summary

`OnAtrBoxPreviewKeyDown` currently marks ALL keys as handled whenever the ATR
fraction TextBox has keyboard focus. This suppresses Backspace, Delete, arrow
keys, and Ctrl combos before they can reach the TextBox's own `KeyDown`
handler, making the box non-editable in practice.

The fix restricts `e.Handled = true` to unmodified alpha/digit keys only —
the only keys the NT8 chart-level instrument-search interceptor reads during
the tunnel phase. Editing keys flow through to the TextBox as designed.

### Exact line range (verified against source)

Lines **3309–3316** (comment block at 3309-3311, method body at 3312-3316).

### BEFORE block (exact source, lines 3309-3316)

```csharp
            // B175: PreviewKeyDown on _atrFractionBox. Marks event handled when box has
            // keyboard focus, blocking NT8 chart-level search interceptor (tunnel phase).
            // CYC=2: base + IsKeyboardFocused check. JS-021: no lock. UI thread only. ASCII-only.
            private void OnAtrBoxPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
            {
                if (_atrFractionBox != null && _atrFractionBox.IsKeyboardFocused)
                    e.Handled = true;
            }
```

### AFTER block (exact replacement, lines 3309-3322)

```csharp
            // B176: PreviewKeyDown on _atrFractionBox. Blocks NT8 chart-level instrument-search
            // interceptor ONLY for unmodified alpha/digit keys (the only keys NT8's search reads).
            // Editing keys (Backspace, Delete, arrows, Ctrl/Shift combos) are NOT marked handled so
            // they reach the TextBox's KeyDown handler. Printable chars reach the TextBox via the
            // independent PreviewTextInput/TextInput event pair (not suppressed by PreviewKeyDown
            // handled flag in standard WPF).
            // CYC=5: null/focus guard(1)+early return(2)+isAlpha(3)+isDigit(4)+noMod(5).
            // JS-021: no lock. UI thread only. ASCII-only.
            private void OnAtrBoxPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
            {
                if (_atrFractionBox == null || !_atrFractionBox.IsKeyboardFocused)
                    return;
                bool isAlpha = e.Key >= System.Windows.Input.Key.A
                            && e.Key <= System.Windows.Input.Key.Z;
                bool isDigit = (e.Key >= System.Windows.Input.Key.D0
                             && e.Key <= System.Windows.Input.Key.D9)
                            || (e.Key >= System.Windows.Input.Key.NumPad0
                             && e.Key <= System.Windows.Input.Key.NumPad9);
                bool noMod   = e.KeyboardDevice.Modifiers
                             == System.Windows.Input.ModifierKeys.None;
                if ((isAlpha || isDigit) && noMod)
                    e.Handled = true;
            }
```

### Method signature involved

```csharp
private void OnAtrBoxPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
```

### CYC delta

| Metric | Value |
|--------|-------|
| CYC before | 2 |
| CYC after  | 5 |
| Delta      | +3  |
| Compliant (≤8) | YES |

CYC breakdown after:
1. Base path
2. `null / !IsKeyboardFocused` guard → early return
3. `isAlpha` boolean expression
4. `isDigit` boolean expression
5. `noMod` boolean expression

### 7-scan checklist

| # | Scan | Pass criterion | Command |
|---|------|---------------|---------|
| SCAN-01 | Lock-free | No `lock(` in changed lines | `grep -c "lock(" src/PropTraderTools/TradeCopierPanel.cs` == 0 |
| SCAN-02 | e.Handled guard scoped | Unconditional `e.Handled = true` absent; handled only inside `(isAlpha \|\| isDigit) && noMod` | Manual diff review |
| SCAN-03 | isAlpha present | Variable `isAlpha` exists in method body | `grep -c "isAlpha" src/PropTraderTools/TradeCopierPanel.cs` >= 1 |
| SCAN-04 | isDigit present | Variable `isDigit` exists in method body | `grep -c "isDigit" src/PropTraderTools/TradeCopierPanel.cs` >= 1 |
| SCAN-05 | noMod present | Variable `noMod` exists in method body | `grep -c "noMod" src/PropTraderTools/TradeCopierPanel.cs` >= 1 |
| SCAN-06 | ASCII-only | No non-ASCII characters in changed lines | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/TradeCopierPanel.cs` returns no hits in lines 3309-3322 |
| SCAN-07 | AtrCalc regression guard | `AtrCalc.cs` NOT modified | `git status src/PropTraderTools/AtrCalc.cs` shows no modifications; or file timestamp unchanged |

### Scope boundaries (DO NOT TOUCH)

- `AtrCalc.cs` — **DO NOT TOUCH** (B175 Wilder's ATR regression guard)
- `CopyEngineTests.cs` — **DO NOT TOUCH**
- Subscription line 3213 (`_atrFractionBox.PreviewKeyDown += OnAtrBoxPreviewKeyDown`) — **DO NOT TOUCH**

### ptt-sync.ps1 note

**Do NOT run ptt-sync.ps1 after B176-1 alone.**
Run it once after BOTH tickets (B176-1 and B176-2) are complete — see B176-2 for the sync command.

---

## SCOPE LOCK -- TICKET B176-2 ONLY
Do NOT implement or reference any other ticket in this session.

---

## Ticket B176-2

**Ticket ID:** B176-2
**File:** `src/PropTraderTools/CopyEngine.cs`
**Mechanism:** `apply_diff` ONLY. Do NOT use `write_file` on any `.cs` file.
**Epic ID:** DW-B159-K5 (P3)
**Spec requirement IDs:** DW-B159-K5

### Summary

Comment at line 3202 references `HasOpenPosition` at line 5507. The actual
current line number of `HasOpenPosition` in `CopyEngine.cs` is **5526**
(confirmed by grep). Update the stale line reference in the comment.
Zero executable lines are changed.

### Exact line range (verified against source)

Line **3202** only.

### BEFORE block (exact source, line 3202)

```csharp
        // Signal 1 (HasOpenPosition, line 5507): position Quantity > 0 (normal case, B159).
```

### AFTER block (exact replacement, line 3202)

```csharp
        // Signal 1 (HasOpenPosition, line 5526): position Quantity > 0 (normal case, B159).
```

### Method signature involved

No method signature change. The comment sits inside the guard block adjacent to
the calling context of `HasOpenPosition`. The method being referenced is:

```csharp
private bool HasOpenPosition(Account acc, Instrument instrument)  // line 5526
```

### CYC delta

| Metric | Value |
|--------|-------|
| CYC before | 0 delta |
| CYC after  | 0 delta |
| Delta      | 0 |

No executable lines changed.

### 7-scan checklist

| # | Scan | Pass criterion | Command |
|---|------|---------------|---------|
| SCAN-01 | 5507 absent | Stale reference removed | `grep -c "line 5507" src/PropTraderTools/CopyEngine.cs` == 0 |
| SCAN-02 | 5526 present at line ~3202 | Correct reference present | `grep -n "line 5526" src/PropTraderTools/CopyEngine.cs` shows line 3202 |
| SCAN-03 | HasOpenPosition at 5526 | Actual method at stated line | `grep -n "private bool HasOpenPosition" src/PropTraderTools/CopyEngine.cs` shows line 5526 |
| SCAN-04 | Zero executable lines changed | Only comment text modified | Diff shows single line changed; no `{`, `}`, expression, or statement added/removed |
| SCAN-05 | Lock-free | No new `lock(` introduced | `grep -c "lock(" src/PropTraderTools/CopyEngine.cs` count unchanged versus baseline |
| SCAN-06 | ASCII-only | No non-ASCII characters in changed line | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` returns no hit at line 3202 |
| SCAN-07 | ptt-sync.ps1 run | Hard links re-synchronised after this ticket | See sync command below; 18 linked files, 0 errors expected |

### ptt-sync.ps1 run instruction

Run this after B176-2 completes (covers both B176-1 and B176-2):

```powershell
powershell -File .\scripts\ptt-sync.ps1
```

Expected output: 18 linked files synchronised, 0 errors.

---

## Execution order

1. Implement **B176-1** (TradeCopierPanel.cs, lines 3309-3316 → 3309-3322).
2. Implement **B176-2** (CopyEngine.cs, line 3202 comment only).
3. Run `powershell -File .\scripts\ptt-sync.ps1` once after step 2.
4. Execute all 7 SCAN checks for each ticket and confirm all pass.
