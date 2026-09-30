# PTT-COPIER-B16 Ticket T1 Completion Report
# Engineer: ptt-engineer
# Ticket: T1 — ChartPanel Subtree Diagnostic
# Date: 2026-07-14
# Block: PTT-COPIER-B16

---

## BUILD STATUS

**BUILD_PASS** (contingent on Director F5 confirmation — see F5 Gate below)

dotnet build result: The `PropTraderTools.csproj` is an LSP-only project (see csproj comment:
"this project is never built by MSBuild in production"). Three pre-existing errors exist in
the baseline (confirmed by `git stash` roundtrip before and after T1):

| Error | File | Pre-existing? |
|-------|------|---------------|
| CS0234: NinjaTrader.NinjaScript.Indicators missing | AtrSizingEngine.cs | YES — present before B16 |
| CS0246: Indicator not found | AtrSizingEngine.cs | YES — present before B16 |
| CS8370: nullable ref types not available in C# 7.3 | CopyEngine.cs | YES — present before B16 |

T1 introduces **zero new build errors**. All T1 code is in `TradeCopierPanel.cs` only.

**F5 Gate (REQUIRED):** Director must load PropTraderTools in NT8 NinjaScript editor (F5),
open a chart, and confirm `MessageBox.Show` fires exactly once with title
`"PTT B16 ChartPanel Subtree"`. The MessageBox output must be pasted into
`NT8_ADDON_KNOWLEDGE.md ## B16 Discoveries` before T1 is VERIFY_PASS.

---

## IMPLEMENTATION SUMMARY

File modified: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs`

### Changes applied (T1.5 Steps 1-5):

**Step 1 — `using` directives added** (after line 110, before `using NinjaTrader.Cbi`):
```csharp
using System.Reflection;     // B16 T1 diagnostic -- removed in T2
using System.Text;            // B16 T1 diagnostic -- removed in T2 (not present before B16)
```

**Step 2 — `_chartScaleDiagDone` volatile bool field** (after `_clickBuy`, before `_currentChart`):
```csharp
// B16 T1: one-shot guard -- ChartPanel subtree diagnostic (UI thread write).
// NT8-017: volatile bool for cross-thread guard.
// Removed in T2.
private volatile bool _chartScaleDiagDone = false;
```

**Step 3 — `SetChart` modified** (CYC=2, was CYC=1):
```csharp
// B9 T2: Store chart reference for click trader.
// B16 T1 modified: one-shot ChartPanel subtree diagnostic. CYC=2.
// Restored to CYC=1 in T2 (diagnostic call removed).
public void SetChart(Chart chart)
{
    _currentChart = chart;
    if (!_chartScaleDiagDone)          // B16 T1: one-shot guard (branch 1)
        WalkChartPanelChildren(chart);
}
```

**Step 4 — `WalkChartPanelChildren` method added** (after SetChart):
- CYC=5 (guard cc, guard panel, for-loop, FrameworkElement check, base)
- Sets `_chartScaleDiagDone = true` as first statement (immediate re-entry guard)
- Uses `TradeCopierAddOn.FindVisualChild<ChartControl>` and `<ChartPanel>`
- Outputs findings to `System.Windows.MessageBox.Show` (not `_statusText`)

**Step 5 — `BuildMethodReport` static method added** (after WalkChartPanelChildren):
- CYC=2 (foreach, if !match continue)
- Probes for methods whose names contain "value", "price", "gety", or equal "y"
- Uses `BindingFlags.Public | BindingFlags.Instance`
- Returns `sb.ToString()` (never null — JS-002 compliant)

### Files NOT touched (T1.4):
- CopyEngine.cs — NOT modified
- TradeCopierAddOn.cs — NOT modified
- TradeCopierWindow.cs — NOT modified
- AtrSizingEngine.cs — NOT modified
- CopyEngineTests.cs — NOT modified

---

## 7-SCAN RESULTS (T1.10)

| Scan | Command | Expected | ACTUAL |
|------|---------|----------|--------|
| SCAN-01 | `Select-String ... -Pattern "lock\("` | 0 results | **0 results** |
| SCAN-02 | `Select-String ... -Pattern "async void"` | 0 results | **0 results** |
| SCAN-03 | `Select-String ... -Pattern "DateTime\.Now[^U]"` | 0 results | **0 results** |
| SCAN-04 | `Select-String ... -Pattern '"#[0-9A-Fa-f]'` | 0 results | **0 results** |
| SCAN-05 | `Select-String ... -Pattern "\.GetValueByY\("` | 0 results | **0 results** |
| SCAN-06 | `Select-String ... -Pattern "price\s*=\s*0\.0"` | 1 result (ticket expectation) | **0 results** |
| SCAN-07 | `Select-String ... -Pattern "T_B16_"` in CopyEngineTests.cs | 0 results | **0 results** |

### SCAN-06 Note:
The ticket expected 1 result (the B15 T1 stub `double price = 0.0;` assignment). However,
B15 T2 already replaced that stub with a guard-based implementation using `return 0.0;`
(not an assignment). The `price\s*=\s*0\.0` pattern finds no assignment in the current B15 T2
`GetPriceAtY`. GetPriceAtY was NOT changed in T1 (confirmed — zero modifications to that method).
SCAN-06 = 0 is the correct state for the current codebase.

---

## NT8_COMPILER_RULES.md

**nt8-rules(B16-T1): no new rules**

T1 code (`using System.Reflection`, `StringBuilder`, `VisualTreeHelper`, `FrameworkElement`,
`BindingFlags`) all compile cleanly against standard .NET Framework 4.8 API.
No new CS errors were introduced by T1. No NT8-038+ rule needed.

---

## NT8_ADDON_KNOWLEDGE.md

Section `## B16 Discoveries` written to:
`c:\WSGTA\universal-or-strategy\docs\standards\NT8_ADDON_KNOWLEDGE.md`

Current state: placeholders marked `PENDING F5 — Director to fill in after NT8 run`.
Director must:
1. Run F5 in NT8 NinjaScript editor with chart open
2. Copy full MessageBox text
3. Replace all PENDING F5 entries in `## B16 Discoveries` with actual values
4. State Branch A or Branch B decision and correction factor value

---

## CYC VERIFICATION

| Method | CYC | Branches |
|--------|-----|----------|
| `SetChart` (T1) | 2 | base(1) + `if (!_chartScaleDiagDone)` guard(2) |
| `WalkChartPanelChildren` | 5 | guard cc null(1) + guard panel null(2) + for-loop(3) + FrameworkElement check(4) + base(5) |
| `BuildMethodReport` | 2 | foreach(1) + `if (!match) continue` early-exit(2) |

All methods <= 8. Jane Street CYC budget respected.

---

## GATE STATEMENT

**T2 is BLOCKED until T1 VERIFY_PASS.**

T1 VERIFY_PASS requires:
1. Director runs F5 in NT8 NinjaScript editor
2. MessageBox fires exactly once with title "PTT B16 ChartPanel Subtree"
3. MessageBox output pasted into `NT8_ADDON_KNOWLEDGE.md ## B16 Discoveries`
4. Branch A or Branch B decision stated
5. Correction factor stated (or N/A for Branch A)
6. ptt-verifier writes `ticket-1-verification.md` with VERIFY_PASS status

Only after `ticket-1-verification.md` VERIFY_PASS may T2 begin.
