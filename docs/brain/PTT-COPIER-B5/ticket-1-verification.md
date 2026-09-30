# PTT-COPIER-B5 — Ticket T1 Verification
**Ticket**: T1 — TradeCopierPanel.cs multi-select ListBox  
**File verified**: `src/PropTraderTools/TradeCopierPanel.cs` (Wave workspace, READ ONLY)  
**Date**: 2026-07-06  
**Verifier**: PTT Verifier (independent, no trust of engineer scan results)  
**Architecture plan**: `docs/brain/PTT-COPIER-B5/02-architecture-plan.md`  
**Completion report**: `docs/brain/PTT-COPIER-B5/ticket-1-completion.md`  

---

## FINAL VERDICT

**VERIFY_FAIL**  
**Violation**: `TradeCopierPanel.cs:195-217` — `OnApplyRule()` CYC = 10, exceeds Jane Street threshold of 8.  
**Requirement**: All methods CYC ≤ 8 (JS-021/RULES_CATALOG, architecture plan Section G).  
**Engineer claim**: CYC = 6 (undercounted: omitted three `if (_statusText != null)` guards and one `||` boolean branch).  
**Action required**: Extract at least one helper method (e.g., `TryGetFollowers()` or `ApplyRuleGuarded()`) to bring `OnApplyRule` to CYC ≤ 8.

---

## Independent Scan Results (S1–S7)

All scans run independently by Verifier. Engineer results NOT accepted.  
Working directory: `c:\WSGTA\universal-or-strategy`

### S1 — No `lock(` usage
```
Command: Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern 'lock\s*\('
Result:  S1_PASS — 0 matches
```

### S2 — No `DateTime.Now` usage
```
Command: Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern 'DateTime\.Now'
Result:  S2_PASS — 0 matches
```

### S3 — No hex colour literals (`0x…`)
```
Command: Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern '0x[0-9A-Fa-f]'
Result:  S3_PASS — 0 matches
```

### S4 — ASCII-only (no non-ASCII bytes)
```
Command: if (Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern '[^\x00-\x7F]') { "FAIL" } else { "PASS" }
Result:  S4_PASS — PASS
```

### S5 — CYC ≤ 8 for all methods  **⚠ FAIL**

```
Method          CYC   Status
-----------     ---   ------
OnInitialize      2   PASS
OnDestroyed       1   PASS
BuildUI           1   PASS
OnToggle          2   PASS
OnTrim            2   PASS
OnFlatten         2   PASS
OnCancel          3   PASS
OnBreakEven       6   PASS
OnApplyRule      10   ** FAIL — exceeds threshold 8 **
OnStatusUpdate    2   PASS
CanExecute        1   PASS
Execute           1   PASS
```

**OnApplyRule CYC breakdown (McCabe strict, all decision points):**

| # | Line | Decision | +CYC |
|---|------|----------|------|
| base | — | method entry | 1 |
| 1 | 198 | `if (_instrument == null)` | +1 |
| 2 | 200 | `if (_statusText != null)` (inner guard) | +1 |
| 3 | 205 | `if (_followersListBox != null)` | +1 |
| 4 | 206 | `foreach (var item in ...)` | +1 |
| 5 | 207 | `if (item is Account acc)` | +1 |
| 6 | 208 | `if (leader == null ...)` | +1 |
| 7 | 208 | `\|\|` boolean operator | +1 |
| 8 | 210 | `if (_statusText != null)` (inner guard) | +1 |
| 9 | 215 | `if (_statusText != null)` (inner guard) | +1 |
| | | **Total CYC** | **10** |

**Engineer's error**: The engineer reported CYC = 6. They omitted:
- Line 200: `if (_statusText != null)` (inner guard inside first return-early branch)
- The `||` boolean operator on line 208 (counts as +1 in McCabe)
- Line 210: `if (_statusText != null)` (inner guard inside second return-early branch)
- Line 215: `if (_statusText != null)` (trailing status-text guard)

The engineer's count of 6 only traced the "happy-path" conditional structure and ignored all `if (_statusText != null)` null guards. McCabe CYC counts every decision point.

### S6 — All B4 `using` directives preserved

```
Line  4: using System;
Line  5: using System.Collections.Generic;   ← B5 addition (correct)
Line  6: using System.Windows;
Line  7: using System.Windows.Controls;
Line  8: using System.Windows.Input;
Line  9: using NinjaTrader.Cbi;
Line 10: using NinjaTrader.Gui;
Line 11: using NinjaTrader.Gui.Chart;
Line 12: using NinjaTrader.Gui.Tools;
Line 13: using NinjaTrader.NinjaScript;
```

All 9 B4 directives present. 1 new directive added (`System.Collections.Generic`). **S6_PASS**

### S7 — Syntax review (balanced delimiters)

```
Open braces:  38  Close braces:  38  Balance: 0
Open parens: 102  Close parens: 102  Balance: 0
Result: S7_PASS — All delimiters balanced; no dangling braces or unmatched brackets
```

---

## Additive Contract Verification (V-A through V-F)

### V-A — Field `_followersListBox` (ListBox) present
```
Line 30: private ListBox _followersListBox;
Result: V-A PASS
```

### V-B — Old field `_followersCombo` (ComboBox) absent
```
Select-String for '_followersCombo': 0 matches
Result: V-B PASS — field was cleanly renamed; no residual references
```

### V-C — `BuildUI()` contains ListBox with `SelectionMode=Extended`, `MaxHeight=80`, wrapped in `ScrollViewer`
```
Line 71: SelectionMode = SelectionMode.Extended
Line 73: MaxHeight = 80            (on ListBox)
Line 79: MaxHeight = 80            (on ScrollViewer)
Line 76: new ScrollViewer { ... Content = _followersListBox }
Result: V-C PASS — all three architecture requirements present
```

### V-D — `OnApplyRule()` iterates `SelectedItems` → `List<Account>` → `.ToArray()`
```
Line 204: var followers = new List<Account>();
Line 206: foreach (var item in _followersListBox.SelectedItems)
Line 214: _engine.AddRule(_instrument.FullName, leader, followers.ToArray());
Result: V-D PASS — pattern matches architecture plan Section D exactly
```

### V-E — All B1–B4 methods present and unremoved
```
Line  32: OnInitialize    PASS
Line  43: OnDestroyed     PASS
Line  49: BuildUI         PASS
Line 160: OnToggle        PASS
Line 167: OnTrim          PASS
Line 173: OnFlatten       PASS
Line 179: OnCancel        PASS
Line 186: OnBreakEven     PASS  (B4 addition)
Line 195: OnApplyRule     PASS  (present; modified per ticket)
Line 219: OnStatusUpdate  PASS
Line 229: RelayCommand    PASS  (nested class)
Result: V-E PASS — no B1–B4 method removed or renamed
```

### V-F — B4 `OnBreakEven` body unchanged (regression check)
```
Line 188: if (_instrument == null) return;              PRESENT
Line 190: if (int.TryParse(_beBufferBox?.Text?.Trim(), out int parsed) && parsed >= 0)  PRESENT
Line 192: _engine.BreakEven(_instrument, ticks);        PRESENT
Result: V-F PASS — OnBreakEven body is byte-identical to B4 baseline
```

---

## Architecture Plan Compliance

| Requirement (02-architecture-plan.md) | Actual | Status |
|---------------------------------------|--------|--------|
| Field rename: `_followersCombo` → `_followersListBox` | Done (line 30) | PASS |
| `BuildUI()`: replace ComboBox with ListBox+ScrollViewer | Done (lines 69–82) | PASS |
| `OnApplyRule()`: multi-select extraction via `SelectedItems` | Done (lines 204–214) | PASS |
| `using System.Collections.Generic` added | Done (line 5) | PASS |
| `_followersCombo` fully removed (no orphan references) | Done | PASS |
| CYC ≤ 8 for all methods (Section G) | OnApplyRule = 10 | **FAIL** |
| No `lock()` usage (JS-021) | 0 matches | PASS |
| No `DateTime.Now` | 0 matches | PASS |
| No hex colour literals | 0 matches | PASS |
| ASCII-only file | No non-ASCII bytes | PASS |
| All B1–B4 methods intact | All 11 present | PASS |
| No B4 regression (OnBreakEven) | Body unchanged | PASS |

---

## Summary

| Scan/Check | Result | Notes |
|------------|--------|-------|
| S1 lock() | PASS | 0 matches |
| S2 DateTime.Now | PASS | 0 matches |
| S3 Hex colours | PASS | 0 matches |
| S4 ASCII-only | PASS | No non-ASCII bytes |
| S5 CYC ≤ 8 | **FAIL** | OnApplyRule = 10 (TradeCopierPanel.cs:195) |
| S6 Using directives | PASS | All 9 B4 directives + 1 new |
| S7 Syntax balanced | PASS | Braces 38/38, Parens 102/102 |
| V-A _followersListBox | PASS | Line 30 |
| V-B _followersCombo removed | PASS | 0 references |
| V-C ListBox+ScrollViewer | PASS | Lines 69–82 |
| V-D SelectedItems→ToArray | PASS | Lines 204–214 |
| V-E All methods present | PASS | 11/11 |
| V-F B4 regression | PASS | OnBreakEven unchanged |

---

## Required Fix

**File**: `src/PropTraderTools/TradeCopierPanel.cs`  
**Violation**: `OnApplyRule()` at lines 195–217, CYC = 10  
**Fix strategy**: Extract helper to reduce CYC to ≤ 8. Example:

```csharp
// Extract this block into a helper to bring OnApplyRule CYC from 10 to ~6:
private List<Account> GetSelectedFollowers()
{
    var followers = new List<Account>();
    if (_followersListBox == null) return followers;
    foreach (var item in _followersListBox.SelectedItems)
        if (item is Account acc) followers.Add(acc);
    return followers;
}
```

This moves 3 decision points (if-listbox-null + foreach + if-is-Account) into the helper, reducing `OnApplyRule` from CYC=10 to CYC=7.

*End of PTT-COPIER-B5 Ticket T1 Verification*

---

## Verification Pass 2

**Date**: 2026-07-06
**Pass**: 2 (Retry 1 — CYC fix applied by engineer)
**Verifier**: PTT Verifier (independent — engineer scan results NOT trusted)
**Source file**: `src/PropTraderTools/TradeCopierPanel.cs` (252 lines, READ ONLY)
**Trigger**: Pass 1 returned VERIFY_FAIL (OnApplyRule CYC = 10). Engineer extracted `GetSelectedFollowers()` helper.

---

### Independent Scan Results (S1–S7)

All scans executed independently by Verifier from `c:\WSGTA\universal-or-strategy`.

#### S1 — No `lock(` usage
```
Command: Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern 'lock\s*\('
Result:  S1_PASS — 0 matches
```

#### S2 — No `DateTime.Now` usage
```
Command: Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern 'DateTime\.Now'
Result:  S2_PASS — 0 matches
```

#### S3 — No hex colour literals (`0x…`)
```
Command: Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern '0x[0-9A-Fa-f]'
Result:  S3_PASS — 0 matches
```

#### S4 — ASCII-only (no non-ASCII bytes)
```
Command: if (Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern '[^\x00-\x7F]') { "S4_FAIL" } else { "S4_PASS" }
Result:  S4_PASS
```

#### S5 — CYC ≤ 8 for all methods  ✅ PASS (both target methods)

Independent McCabe strict recount from file source:

**`GetSelectedFollowers()` — lines 195–202**

| # | Line | Decision point | +CYC |
|---|------|---------------|------|
| base | — | method entry | 1 |
| 1 | 198 | `if (_followersListBox == null)` | +1 |
| 2 | 199 | `foreach (var item in ...)` | +1 |
| 3 | 200 | `if (item is Account acc)` | +1 |
| | | **Total CYC** | **4** ✅ |

**`OnApplyRule()` — lines 204–223**

| # | Line | Decision point | +CYC |
|---|------|---------------|------|
| base | — | method entry | 1 |
| 1 | 207 | `if (_instrument == null)` | +1 |
| 2 | 209 | `if (_statusText != null)` (inner guard, first early-return) | +1 |
| 3 | 214 | `if (leader == null \|\| followers.Length == 0)` | +1 |
| 4 | 214 | `\|\|` boolean operator | +1 |
| 5 | 216 | `if (_statusText != null)` (inner guard, second early-return) | +1 |
| 6 | 221 | `if (_statusText != null)` (trailing success guard) | +1 |
| | | **Total CYC** | **8** ✅ |

**Key difference from Pass 1**: The `if (_followersListBox != null)` + `foreach` + `if (item is Account)` block (3 decision points) has been extracted into `GetSelectedFollowers()`. `OnApplyRule` no longer contains those points — it delegates with a single call on line 213. CYC dropped from 10 → 8. Threshold ≤ 8 is now exactly met.

Full method table:

```
Method                  CYC   Status
--------------------    ---   ------
OnInitialize              2   PASS
OnDestroyed               1   PASS
BuildUI                   1   PASS
OnToggle                  2   PASS
OnTrim                    2   PASS
OnFlatten                 2   PASS
OnCancel                  3   PASS
OnBreakEven               6   PASS
GetSelectedFollowers      4   PASS  (new helper, Pass 2)
OnApplyRule               8   PASS  (was 10 in Pass 1 — FIXED)
OnStatusUpdate            2   PASS
CanExecute                1   PASS
Execute                   1   PASS
```

#### S6 — All B4+B5 `using` directives preserved
```
Line  4: using System;
Line  5: using System.Collections.Generic;   (B5 addition — correct)
Line  6: using System.Windows;
Line  7: using System.Windows.Controls;
Line  8: using System.Windows.Input;
Line  9: using NinjaTrader.Cbi;
Line 10: using NinjaTrader.Gui;
Line 11: using NinjaTrader.Gui.Chart;
Line 12: using NinjaTrader.Gui.Tools;
Line 13: using NinjaTrader.NinjaScript;
Result:  S6_PASS — all 10 directives present (9 baseline + 1 B5 addition)
```

#### S7 — Syntax check (brace / paren balance)
```
Command: PowerShell char-count balance check
Open braces:  39  Close braces:  39  Balance: 0
Open parens: 105  Close parens: 105  Balance: 0
Result:  S7_PASS — all delimiters balanced
```
*(Note: Pass 1 reported 38/38 and 102/102; new counts reflect the 9 lines added by GetSelectedFollowers helper — consistent.)*

---

### Additive Contract Verification (V-A through V-D)

#### V-A — `GetSelectedFollowers()` present (was not in B4)?
```
Line 195: private Account[] GetSelectedFollowers()
Result:   V-A PASS — new helper present, correct return type Account[]
```

#### V-B — `OnApplyRule()` calls `GetSelectedFollowers()` and CYC ≤ 8?
```
Line 213: var followers = GetSelectedFollowers();
CYC:      8 (see S5 above)
Result:   V-B PASS — call present; CYC = 8, exactly at threshold
```

#### V-C — All B1–B4 methods untouched?
```
OnInitialize   (32–41)  : PASS — unchanged
OnDestroyed    (43–47)  : PASS — unchanged
BuildUI        (49–158) : PASS — followers block (69-82) was B5 change; rest unchanged
OnToggle       (160–165): PASS — unchanged
OnTrim         (167–171): PASS — unchanged
OnFlatten      (173–177): PASS — unchanged
OnCancel       (179–183): PASS — unchanged
OnBreakEven    (186–193): PASS — unchanged (B4 addition)
OnStatusUpdate (225–232): PASS — unchanged
RelayCommand   (235–249): PASS — unchanged
Result:  V-C PASS — all B1–B4 methods intact
```

#### V-D — No regression in `OnBreakEven`?
```
Line 188: if (_instrument == null) return;                                          PRESENT
Line 189: int ticks = 2;                                                             PRESENT
Line 190: if (int.TryParse(_beBufferBox?.Text?.Trim(), out int parsed) && parsed >= 0)  PRESENT
Line 191:     ticks = parsed;                                                        PRESENT
Line 192: _engine.BreakEven(_instrument, ticks);                                    PRESENT
Result:  V-D PASS — OnBreakEven body byte-identical to B4 baseline
```

---

### Architecture Plan Compliance (Pass 2 delta)

| Requirement | Pass 1 | Pass 2 | Delta |
|-------------|--------|--------|-------|
| CYC ≤ 8 — `OnApplyRule` | FAIL (CYC=10) | **PASS (CYC=8)** | ✅ FIXED |
| CYC ≤ 8 — `GetSelectedFollowers` | N/A (not extracted) | **PASS (CYC=4)** | ✅ NEW |
| All other requirements | PASS | PASS | unchanged |

---

### Scan Summary (Pass 2)

| Scan/Check | Result | Notes |
|------------|--------|-------|
| S1 lock() | PASS | 0 matches |
| S2 DateTime.Now | PASS | 0 matches |
| S3 Hex colours | PASS | 0 matches |
| S4 ASCII-only | PASS | No non-ASCII bytes |
| S5 CYC ≤ 8 | **PASS** | GetSelectedFollowers CYC=4; OnApplyRule CYC=8 (was 10) |
| S6 Using directives | PASS | 10 directives (9 B4 baseline + 1 B5) |
| S7 Syntax balanced | PASS | Braces 39/39, Parens 105/105 |
| V-A GetSelectedFollowers present | PASS | Line 195 |
| V-B OnApplyRule calls helper + CYC ≤ 8 | PASS | Line 213; CYC=8 |
| V-C All B1–B4 methods untouched | PASS | 10/10 |
| V-D OnBreakEven no regression | PASS | Body unchanged |

---

## FINAL VERDICT (Pass 2)

**VERIFY_PASS**

All 7 scans clean. `GetSelectedFollowers()` extracted correctly (CYC=4). `OnApplyRule()` CYC reduced from 10 → 8, exactly at the ≤ 8 Jane Street threshold. No B1–B4 regressions. No lock(), no DateTime.Now, no hex literals, no non-ASCII bytes.

*End of Verification Pass 2*
