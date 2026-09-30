# Tickets -- B177 DW-B176-TEXTBOX-FIX-PIPELINE
# Phase 3 -- PTT Architect (Ticket Generation)
# Block: B177
# Plan status: REVIEW_PASS (Cycle 2)
# Ticket count: 1

---

## TICKET B177-1 -- Validate B176-FIX Repair in TradeCopierPanel.cs lines 3309-3344

**Title**: Validate B176-FIX repair in TradeCopierPanel.cs lines 3309-3344
**Status**: OPEN
**Epic closed**: DW-B176-TEXTBOX-FIX (P0), DW-B176-SIM (P1)
**New deferred**: DW-B177-CYC (P2)

---

### SCOPE LOCK

```
SCOPE: B177-1 ONLY. Do NOT edit any .cs file. Read-only validation.
```

This ticket is **validate-only**. No source files are created, modified, or deleted.
The engineer's sole deliverable is `docs/brain/B177/ticket-1-completion.md`.

---

### SPEC REQUIREMENT IDs SATISFIED

| Requirement ID | Priority | Description | Closed By |
|----------------|----------|-------------|-----------|
| DW-B176-TEXTBOX-FIX | P0 | ATR% TextBox digit/period input was broken in NT8 (search dialog opened instead). B176-FIX applied manual injection at caret. B177-1 verify pass constitutes definitive pipeline closure. | B177-1 VERIFY_PASS |
| DW-B176-SIM | P1 | Director SIM PASS required to confirm the repair works in NT8 environment. Director SIM was confirmed post-B176-FIX application. B177-1 documents this closure formally. | Director SIM CONFIRMED (pre-B177) |

---

### FILE UNDER VALIDATION

| File | Lines | Action |
|------|-------|--------|
| `src/PropTraderTools/TradeCopierPanel.cs` | 3309-3344 | **READ ONLY** -- no edits under any circumstances |

---

### EXACT LINE NUMBERS (SOURCE TRUTH -- confirmed by Phase 3 read)

| Element | Line(s) | Content |
|---------|---------|---------|
| Comment block: B176-FIX tag | 3309 | `// B176-FIX: PreviewKeyDown on _atrFractionBox.` |
| Source comment (incorrect CYC=8) | 3316-3317 | `// CYC=8: ...` -- **incorrect; actual CYC=13** |
| Method signature | 3318 | `private void OnAtrBoxPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)` |
| null/focus guard | 3320 | `if (_atrFractionBox == null \|\| !_atrFractionBox.IsKeyboardFocused)` |
| noMod declaration | 3322 | `bool noMod = e.KeyboardDevice.Modifiers == System.Windows.Input.ModifierKeys.None;` |
| noMod guard | 3323 | `if (!noMod)` |
| isAlpha declaration | 3325 | `bool isAlpha = e.Key >= ... Key.A && e.Key <= ... Key.Z;` |
| isDigitD declaration | 3326 | `bool isDigitD = e.Key >= ... Key.D0 && e.Key <= ... Key.D9;` |
| isDigitN declaration | 3327 | `bool isDigitN = e.Key >= ... Key.NumPad0 && e.Key <= ... Key.NumPad9;` |
| isPeriod declaration | 3328 | `bool isPeriod = e.Key == ... Key.OemPeriod \|\| e.Key == ... Key.Decimal;` |
| Pass-through guard | 3329-3330 | `if (!isAlpha && !isDigitD && !isDigitN && !isPeriod) return;` |
| e.Handled = true | 3331 | `e.Handled = true;` -- exactly once, after pass-through guard |
| Alpha early-return | 3332-3333 | `if (isAlpha) return;` |
| isPeriod ternary | 3335 | `char c = isPeriod ? '.'` |
| isDigitD ternary | 3336 | `: isDigitD ? (char)('0' + (e.Key - System.Windows.Input.Key.D0))` |
| isDigitN (else branch) | 3337 | `: (char)('0' + (e.Key - System.Windows.Input.Key.NumPad0));` |
| SelectionStart | 3338 | `int sel = _atrFractionBox.SelectionStart;` |
| SelectionLength | 3339 | `int len = _atrFractionBox.SelectionLength;` |
| Text null-coalesce | 3340 | `string t = _atrFractionBox.Text ?? string.Empty;` |
| Selection removal | 3341 | `if (len > 0) t = t.Remove(sel, len);` |
| Text.Insert injection | 3342 | `_atrFractionBox.Text = t.Insert(sel, c.ToString());` |
| CaretIndex advance | 3343 | `_atrFractionBox.CaretIndex = sel + 1;` |
| Method close | 3344 | `}` |

---

### 15-POINT VALIDATION CHECKLIST

The engineer must evaluate each check against source lines 3309-3344 and record
PASS or FAIL with evidence in `docs/brain/B177/ticket-1-completion.md`.

| # | Check | Expected | Source Reference |
|---|-------|----------|-----------------|
| 01 | Comment tag is "B176-FIX" (not B175 or B176) | `// B176-FIX:` present at line 3309 | Line 3309 |
| 02 | noMod check present; returns early for modified keys | `bool noMod` at line 3322; `if (!noMod) return;` at lines 3323-3324 | Lines 3322-3324 |
| 03 | isAlpha, isDigitD, isDigitN, isPeriod all declared | All four `bool` declarations present | Lines 3325-3328 |
| 04 | Pass-through guard present: keys not in intercept list return immediately | `if (!isAlpha && !isDigitD && !isDigitN && !isPeriod) return;` at lines 3329-3330 | Lines 3329-3330 |
| 05 | `e.Handled = true` appears exactly ONCE, AFTER pass-through guard, BEFORE injection | Exactly one occurrence at line 3331; guards at 3320, 3323, 3329 precede it; injection at 3335-3343 follows it | Line 3331 |
| 06 | Alpha early-return present: alpha blocked from NT8, no inject | `if (isAlpha) return;` at lines 3332-3333; no injection executed for alpha path | Lines 3332-3333 |
| 07 | OemPeriod AND Decimal both present in isPeriod | `Key.OemPeriod \|\| e.Key == ... Key.Decimal` at line 3328 | Line 3328 |
| 08 | NumPad0..NumPad9 present in isDigitN | `e.Key >= ... Key.NumPad0 && e.Key <= ... Key.NumPad9` at line 3327 | Line 3327 |
| 09 | Injection block present: SelectionStart, SelectionLength, Text.Insert, CaretIndex | All four present at lines 3338, 3339, 3342, 3343 | Lines 3338-3343 |
| 10 | `if (len > 0) t = t.Remove(sel, len)` present (selection replacement) | Exactly this pattern at line 3341 | Line 3341 |
| 11 | CYC = 13 per independent McCabe count (NOT 8 -- source comment at 3316-3317 is wrong) | Independent count yields 13 (see CYC breakdown section below) | Lines 3316-3317 note; method 3318-3344 |
| 12 | No `lock(` in lines 3309-3344 | Zero occurrences of `lock(` | Lines 3309-3344 |
| 13 | No non-ASCII characters in lines 3309-3344 | All chars are ASCII (0x00-0x7F) | Lines 3309-3344 |
| 14 | AtrCalc.cs not modified (git status: Untracked `??`, not `M` or `D`) | `?? src/PropTraderTools/AtrCalc.cs` in `git status` | git status output |
| 15 | ptt-sync.ps1 re-run: 18 linked, 0 errors | `powershell -File .\scripts\ptt-sync.ps1` output shows "18 linked, 0 errors" | ptt-sync.ps1 output |

---

### CYC BREAKDOWN -- INDEPENDENT McCABE COUNT

Method: `OnAtrBoxPreviewKeyDown` (lines 3318-3344)
Standard: JS-013 (CYC <= 8)
**Actual: CYC = 13** -- JS-013 COMPLIANCE FINDING

> **NOTE**: The source comment at lines 3316-3317 states `CYC=8`. This is **incorrect**.
> Source comments do not override the authoritative standard. The independent McCabe count below
> is the definitive count for compliance purposes.

| # | Line | Construct | +CYC | Running Total |
|---|------|-----------|------|---------------|
| base | -- | base count | +1 | 1 |
| 1 | 3320 | `if (_atrFractionBox == null \|\| ...)` -- `if` keyword | +1 | 2 |
| 2 | 3320 | `\|\|` logical-or in null/focus compound | +1 | 3 |
| 3 | 3323 | `if (!noMod)` | +1 | 4 |
| 4 | 3329 | `if (!isAlpha && !isDigitD && !isDigitN && !isPeriod)` -- `if` keyword | +1 | 5 |
| 5 | 3329 | `&&` (first: `!isAlpha && !isDigitD`) | +1 | 6 |
| 6 | 3329 | `&&` (second: `!isDigitD && !isDigitN`) | +1 | 7 |
| 7 | 3329 | `&&` (third: `!isDigitN && !isPeriod`) | +1 | 8 |
| 8 | 3332 | `if (isAlpha)` | +1 | 9 |
| 9 | 3335 | `?:` -- isPeriod ternary (first branch) | +1 | 10 |
| 10 | 3336 | `?:` -- isDigitD ternary (second branch) | +1 | 11 |
| 11 | 3340 | `??` -- null-coalesce on `.Text` | +1 | 12 |
| 12 | 3341 | `if (len > 0)` -- selection removal | +1 | 13 |

**Total: 12 decision points + 1 base = CYC = 13**
Exceeds JS-013 limit of 8 by **5 points**.

---

### JS-013 COMPLIANCE FINDING

```
CYC=13 is a JS-013 compliance finding. Source is already deployed and confirmed by Director SIM.
No source change in B177. DW-B177-CYC deferred for future CYC reduction via helper extraction.
```

**Proposed future reduction** (DW-B177-CYC):
Extract lines 3334-3343 (char-injection body) into a `private void InjectAtrChar(char c)` helper.
This reduces the outer method `OnAtrBoxPreviewKeyDown` to CYC ≤ 8 and the helper to CYC ≤ 3.
Requires `ptt-sync.ps1` run post-edit. Not in scope for B177.

---

### 7-SCAN CHECKLIST (ENGINEER CONTRACT)

These 7 scans must be executed and all results recorded in `docs/brain/B177/ticket-1-completion.md`.

| Scan | Command | Expected Result |
|------|---------|----------------|
| SCAN-01 | `grep "lock(" src/PropTraderTools/TradeCopierPanel.cs` (inspect lines 3309-3344 in output) | Zero matches in lines 3309-3344 (JS-021 PASS) |
| SCAN-02 | Independent McCabe CYC count of `OnAtrBoxPreviewKeyDown` (lines 3318-3344) | CYC = 13 (matches plan; JS-013 FINDING -- note source comment at 3316-3317 is wrong) |
| SCAN-03 | Inspect lines 3309-3344 for any character with code > 0x7F (non-ASCII) | Zero non-ASCII characters found |
| SCAN-04 | `grep "e.Handled = true" src/PropTraderTools/TradeCopierPanel.cs` (count in method scope) | Exactly 1 occurrence within `OnAtrBoxPreviewKeyDown` (line 3331) |
| SCAN-05 | Inspect line 3328 for `Key.OemPeriod` and `Key.Decimal` | Both present: `Key.OemPeriod \|\| e.Key == ... Key.Decimal` |
| SCAN-06 | Inspect line 3327 for `Key.NumPad0` and `Key.NumPad9` | Both present: `Key.NumPad0` (lower bound) and `Key.NumPad9` (upper bound) |
| SCAN-07 | `powershell -File .\scripts\ptt-sync.ps1` | 18 linked, 0 errors |

---

### SCOPE BOUNDARY (MUST NOT TOUCH)

The following files are **explicitly out of scope** for B177-1.
Any modification to these files by the engineer is a protocol violation.

| File | Reason |
|------|--------|
| `src/PropTraderTools/TradeCopierPanel.cs` | Validate-only -- READ is permitted, WRITE is banned |
| `src/PropTraderTools/AtrCalc.cs` | B175 regression guard -- must remain Untracked |
| `src/PropTraderTools/CopyEngineTests.cs` | Not in scope for B177 |
| `src/PropTraderTools/CopyEngine.cs` | Not in scope for B177 |
| All other `.cs` files | No source changes permitted in B177 |
| All other source files | No changes permitted in B177 |

---

### COMPLETION ARTIFACT SPEC

The engineer must produce exactly one output file:

**File**: `docs/brain/B177/ticket-1-completion.md`

**Required contents** (all fields mandatory -- incomplete artifact = TICKET_REVIEW_FAIL):

1. **All 15 validation checks** with PASS/FAIL status and evidence line for each.
2. **All 7 scan results** (SCAN-01 through SCAN-07) with raw command output or equivalent evidence.
3. **Summary statement** (verbatim):
   `"B177-1 VALIDATE_PASS -- no .cs files modified"`
4. **Explicit confirmation**:
   `"AtrCalc.cs: Untracked (not modified)"`
5. **Explicit confirmation**:
   `"ptt-sync.ps1: 18 linked, 0 errors"`
6. **New deferred item declaration**:
   `DW-B177-CYC (P2) -- CYC=13 in OnAtrBoxPreviewKeyDown exceeds JS-013 limit of 8 by 5 points. OPEN.`

---

### METHOD SIGNATURE UNDER VALIDATION (READ ONLY)

```csharp
// src/PropTraderTools/TradeCopierPanel.cs line 3318
private void OnAtrBoxPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
```

No changes to this signature. No overloads. No new tests (validate-only block).

---

*Produced by ptt-architect -- Phase 3 (Ticket Generation)*
*Block: B177 | Epic: DW-B176-TEXTBOX-FIX-PIPELINE*
*Plan cycle: 2 (REVIEW_PASS)*
*Tickets: 1 (B177-1 -- validate-only)*
