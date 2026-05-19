# CODEX HANDOFF: Phase 7 -- UI Keyboard Recovery & Latency Dashboard

> **Author**: P3 Architect (Claude) | **Target**: P4 Engineer (Codex/Jules)
> **Build**: 1108 | **Branch**: `build/1105-monolith`
> **Risk**: LOW -- proven V12_001 port + diagnostic-only append

---

## SURGICAL EDIT LIST (2 edits, 2 files)

---

### E1 -- Manual Text Pipeline (Keyboard Recovery)

**File**: `src/V12_002.UI.Panel.Helpers.cs`

**FIND** (lines 76-82):
```csharp
            tb.PreviewKeyDown += (s, e) =>
            {
                // Build 1106-C: Block chart-hijack for ALL config inputs
                if (e.Key != Key.Tab && e.Key != Key.Enter && e.Key != Key.Escape)
                    e.Handled = true;
            };
            return tb;
```

**REPLACE**:
```csharp
            // Phase 7 [KB-R1]: Manual Text Pipeline -- soaks chart-level keyboard hijack
            // while explicitly managing TextBox content (port from V12_001 baseline).
            tb.PreviewKeyDown += (s, e) =>
            {
                // Let Tab/Enter/Escape bubble for navigation
                if (e.Key == Key.Tab || e.Key == Key.Enter || e.Key == Key.Escape)
                    return;

                // Stop event from bubbling to NinjaTrader chart - prevents symbol search
                e.Handled = true;

                // Manually handle the key input for the TextBox
                TextBox textBox = s as TextBox;
                if (textBox == null) return;

                string keyChar = "";
                if (e.Key >= Key.D0 && e.Key <= Key.D9)
                    keyChar = ((char)('0' + (e.Key - Key.D0))).ToString();
                else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
                    keyChar = ((char)('0' + (e.Key - Key.NumPad0))).ToString();
                else if (e.Key == Key.Back && textBox.Text.Length > 0 && textBox.SelectionStart > 0)
                {
                    int pos = textBox.SelectionStart;
                    textBox.Text = textBox.Text.Remove(pos - 1, 1);
                    textBox.SelectionStart = pos - 1;
                    return;
                }
                else if (e.Key == Key.Delete && textBox.SelectionStart < textBox.Text.Length)
                {
                    int pos = textBox.SelectionStart;
                    textBox.Text = textBox.Text.Remove(pos, 1);
                    textBox.SelectionStart = pos;
                    return;
                }
                else if (e.Key == Key.OemPeriod || e.Key == Key.Decimal)
                    keyChar = ".";
                else if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
                    keyChar = "-";
                else if (e.Key == Key.Space)
                    keyChar = " ";
                else
                    return;  // Ignore other keys

                int caret = textBox.SelectionStart;
                textBox.Text = textBox.Text.Insert(caret, keyChar);
                textBox.SelectionStart = caret + 1;
            };
            tb.GotKeyboardFocus += (s, e) =>
            {
                // Stop bubbling to prevent NT8 chart keyboard shortcuts
                e.Handled = true;
            };
            return tb;
```

---

### E2 -- Latency Dashboard Timing Footer

**File**: `src/V12_002.SIMA.Dispatch.cs`

**FIND** (lines 487-489):
```csharp
                report.Append(dispatchLog.ToString());
                report.AppendLine("+==============================================================+");
                Print(report.ToString().TrimEnd());
```

**REPLACE**:
```csharp
                report.Append(dispatchLog.ToString());
                report.AppendLine("+--------------------------------------------------------------+");
                report.AppendLine("|  TIMING SUMMARY                                              |");
                report.AppendLine("+--------------------------------------------------------------+");
                report.AppendLine(string.Format("|  Setup Phase:  {0,8:F3} ms  |  Fleet Loop:  {1,8:F3} ms       |", setupMs, loopMs));
                report.AppendLine(string.Format("|  Total Elapsed: {0,8:F3} ms                                  |", totalMs));
                report.AppendLine("+==============================================================+");
                Print(report.ToString().TrimEnd());
```

---

## SELF-AUDIT CHECKLIST (P4 Engineer)

After applying both edits, run:

1. **Compile**: Zero errors, zero warnings in NinjaTrader 8
2. **ASCII scan**: `python check_ascii.py src/V12_002.UI.Panel.Helpers.cs src/V12_002.SIMA.Dispatch.cs`
3. **Lock audit**: `grep -n "lock(stateLock)" src/V12_002.UI.Panel.Helpers.cs src/V12_002.SIMA.Dispatch.cs` -- must return zero
4. **Keyboard verification (VM)**:
   - Click any Price/Offset/Trail TextBox on V12 panel
   - Type `1234.56` -- characters must appear
   - Backspace removes last char, Delete removes char at caret
   - NumPad keys work identically to number row
   - Tab moves focus to next field (not blocked)
   - NinjaTrader symbol search must NOT activate during typing
5. **Latency verification**: Trigger a market dispatch, verify Output Window shows:
   ```
   +--------------------------------------------------------------+
   |  TIMING SUMMARY                                              |
   +--------------------------------------------------------------+
   |  Setup Phase:     X.XXX ms  |  Fleet Loop:     X.XXX ms       |
   |  Total Elapsed:    X.XXX ms                                  |
   +==============================================================+
   ```
6. **Commit**: `git commit -m "fix(phase-7): manual text pipeline + latency dashboard metrics"`
