# Architecture Plan -- B177 DW-B176-TEXTBOX-FIX-PIPELINE
# Phase 1 -- PTT Architect
# Block: B177
# Status: PLAN_COMPLETE (CYCLE 2 -- REVIEW_FAIL CORRECTION)

---

## 1. LANE-SPLIT GATE

| Q# | Question | Answer | Result |
|----|----------|--------|--------|
| Q1 | Same method or within 50 lines? | YES -- single method (OnAtrBoxPreviewKeyDown, 36 lines) | **STOP -- SINGLE-PIPELINE** |
| Q2 | Fix B design depends on Fix A final design? | N/A -- only one fix | SINGLE-PIPELINE |
| Q3 | Each fix has standalone value if the other is blocked? | N/A -- only one fix | SINGLE-PIPELINE |
| Q4 | Each fix has an independent SIM verification path? | N/A -- only one fix | SINGLE-PIPELINE |

**LANE-SPLIT GATE RESULT: SINGLE-PIPELINE**

---

## 2. Root Cause Analysis

The B176 pipeline produced `OnAtrBoxPreviewKeyDown` with `e.Handled = true` applied to unmodified
alpha/digit keys, theorising that WPF's TextInput event fires independently of PreviewKeyDown's
Handled flag. Two root causes explain why that theory failed in NT8:

### Root Cause A -- NT8 TextInput Suppression

**Standard WPF behavior**: setting `e.Handled = true` in `PreviewKeyDown` suppresses only the
tunnelling PreviewKeyDown event. The bubbling `TextInput` event fires independently, because
`TextInput` originates from a separate IME composition path, not from the key-down tunnel.

**NT8 embedded WPF host behavior (empirically confirmed by Director SIM)**: NT8 wraps its chart
panel in a custom WPF host that interposes a chart-level key router between the WPF dispatcher and
the control tree. When `e.Handled = true` is detected on a `PreviewKeyDown` event, NT8's router
treats the entire key event as consumed and does not generate a `TextInput` event for that key.
This collapses the standard WPF two-path model into a single path: `e.Handled = true` in
`PreviewKeyDown` kills TextInput too.

**Effect**: Digit keys (D0-D9, NumPad0-NumPad9) set `e.Handled = true` in the B176 handler, so
they were blocked from NT8's search dialog -- but they also never reached the TextBox via
TextInput. The ATR% TextBox remained non-functional for digit input.

### Root Cause B -- OemPeriod / Decimal Not in Intercept List

The B176 intercept list included `Key.A` through `Key.Z` and `Key.D0` through `Key.D9` /
`Key.NumPad0` through `Key.NumPad9`, but did NOT include `Key.OemPeriod` or `Key.Decimal`.

**Effect**: Pressing `.` (period) bypassed the guard, `e.Handled` was never set for it, and NT8's
chart-level search interceptor received the event -- opening the NT8 instrument-search dialog
instead of inserting a decimal point into the TextBox.

---

## 3. Repair Mechanism -- Manual Character Injection at Caret

The B176-FIX (direct repair, lines 3309-3344 in TradeCopierPanel.cs) resolves both root causes
via **manual character injection**:

1. **Block NT8 for all printable-field keys** (`isAlpha || isDigitD || isDigitN || isPeriod`):
   set `e.Handled = true`. This covers `OemPeriod` and `Decimal` -- fixing Root Cause B.

2. **For alpha keys**: `e.Handled = true` then early return. Alpha is not valid in a numeric
   field; suppressing NT8 without injecting is correct. No TextBox change needed.

3. **For digit and period keys**: compute the printable character `c`:
   - `isPeriod` -> `'.'`
   - `isDigitD` -> `(char)('0' + (e.Key - Key.D0))`  (D0=0x22; D0..D9 maps to '0'..'9')
   - else (isDigitN) -> `(char)('0' + (e.Key - Key.NumPad0))`

4. **Inject at caret**:
   - `sel = _atrFractionBox.SelectionStart` -- current insertion point.
   - `len = _atrFractionBox.SelectionLength` -- length of any current selection.
   - `t = _atrFractionBox.Text ?? string.Empty` -- safe read (null-coalesce).
   - If `len > 0`: `t = t.Remove(sel, len)` -- replace selected text with new char.
   - `_atrFractionBox.Text = t.Insert(sel, c.ToString())` -- insert char at caret.
   - `_atrFractionBox.CaretIndex = sel + 1` -- advance caret one position right.

5. **Editing keys** (Backspace, Delete, Left/Right arrows): NOT in intercept list (`!isAlpha &&
   !isDigitD && !isDigitN && !isPeriod` is true) -- early return at pass-through guard (line
   3329-3330). These keys reach the TextBox's native `KeyDown` handler unchanged. Backspace and
   Delete continue to work as before.

**Result**: Digits and period appear in the TextBox exactly as typed. NT8's search interceptor
never fires for any of these keys. Editing keys are unaffected.

---

## 4. CYC = 13 Breakdown -- JS-013 COMPLIANCE FINDING

Method: `OnAtrBoxPreviewKeyDown` (lines 3318-3344)
Limit: 8 | Actual: **13** | Status: **KNOWN_VIOLATION -- JS-013 FINDING (see rationale below)**

> **CYCLE 2 CORRECTION**: The previous plan (Cycle 1) claimed CYC=8 by undercounting logical
> operators in compound predicates and omitting the `??` operator. The correct McCabe count
> per RULES_CATALOG JS-013 (each `if`, `||`, `&&`, `?:`, `??` = +1) is CYC=13.
> The source code comment at line 3316-3317 stating "CYC=8" is **incorrect** and does not
> override the authoritative standard. Source comments do not gate compliance -- JS-013 does.

### Authoritative McCabe Count (CYC_METHODOLOGY.md + JS-013)

| # | Line | Construct | +CYC | Running Total |
|---|------|-----------|------|---------------|
| base | -- | base count | +1 | 1 |
| 1 | 3320 | `if (_atrFractionBox == null \|\| ...)` -- `if` | +1 | 2 |
| 2 | 3320 | `\|\|` in null/focus compound | +1 | 3 |
| 3 | 3323 | `if (!noMod)` | +1 | 4 |
| 4 | 3329 | `if (!isAlpha && !isDigitD && !isDigitN && !isPeriod)` -- `if` | +1 | 5 |
| 5 | 3329 | `&&` (first: `!isDigitD`) | +1 | 6 |
| 6 | 3329 | `&&` (second: `!isDigitN`) | +1 | 7 |
| 7 | 3329 | `&&` (third: `!isPeriod`) | +1 | 8 |
| 8 | 3332 | `if (isAlpha)` | +1 | 9 |
| 9 | 3335 | `?:` (isPeriod ternary -- first branch) | +1 | 10 |
| 10 | 3336 | `?:` (isDigitD ternary -- second branch) | +1 | 11 |
| 11 | 3340 | `??` (null-coalesce on `.Text`) | +1 | 12 |
| 12 | 3341 | `if (len > 0)` -- selection removal | +1 | 13 |

**Total CYC = 13** (exceeds JS-013 limit of 8 by **5 points**).

### JS-013 Compliance Finding Rationale

This is a **validate-only** block. B177 does NOT modify source. The code at lines 3318-3344
is the final, Director-SIM-confirmed working implementation applied outside the pipeline.
Since B177 cannot change source code:

- The CYC=13 violation is documented here as a **known compliance finding**.
- It is carried forward as deferred item **DW-B177-CYC** (see Section 8).
- A future block will reduce CYC to ≤8 by extracting the char-injection body
  (lines 3334-3343) into a `InjectAtrChar(char c)` helper method, reducing the outer
  method to CYC≤8 and the helper to CYC≤3.
- The working behaviour of the method is not in question; this is a structural metric
  violation only.

---

## 5. Jane Street Compliance Table

| Rule | Status | Evidence |
|------|--------|----------|
| No `lock()` introduced | PASS | `grep "lock(" lines 3309-3344` -- 0 matches |
| No Unicode / non-ASCII in changed strings | PASS | Only string literal is `c.ToString()` producing '0'-'9' or '.'; `string.Empty` -- all ASCII |
| No `DateTime.Now` | PASS | Not present in lines 3309-3344 |
| CYC <= 8 (JS-013) | **FINDING** | CYC=13 (exceeds limit by 5). Carried as DW-B177-CYC. Source already deployed, Director SIM confirmed working. No source change in B177. Reduction deferred to future block. |
| `e.Handled = true` exactly once, inside guard | PASS | Line 3331 only; guarded by null/focus (3320), noMod (3323), pass-through (3329) |
| No unconditional `e.Handled = true` | PASS | Three early-return guards precede line 3331 |
| AtrCalc.cs not modified (B175 regression guard) | PASS | Untracked in git status (`?? src/PropTraderTools/AtrCalc.cs`); not in scope; not touched by B177 |
| CopyEngineTests.cs not modified | PASS | Not in scope; `M src/PropTraderTools/CopyEngineTests.cs` is a pre-existing modification unrelated to B177 |

---

## 6. Why No New Code Change Is Needed

The B176-FIX repair was applied directly to `TradeCopierPanel.cs` (lines 3309-3344) outside the
pipeline after Director SIM confirmed the B176 pipeline output was still broken. The Director's
SIM verified:

- Digit keys (0-9 via D-row and NumPad) type correctly into the ATR% TextBox.
- Period (`.`) inserts a decimal point; it does NOT open NT8's search dialog.
- Backspace and Delete continue to work (editing-key pass-through is intact).
- The ATR pts value updates correctly when the box is committed (B175 regression intact).

The source at lines 3309-3344 is the final, confirmed-working implementation. B177 does NOT
re-implement, modify, or refactor this code. The pipeline's sole purpose is to validate and
formally document the repair, and to formally close the deferred items.

---

## 7. Pipeline Scope: 1 Ticket (B177-1), Validate-Only

**B177-1**: Read lines 3309-3344, verify all checklist items pass, produce verification report.
No source edits. No new files. No new tests. No ptt-sync.ps1 run (no source change).

### Ticket Summary Table

| Ticket | File | Change | CYC Before | CYC After | Delta |
|--------|------|--------|------------|-----------|-------|
| B177-1 | TradeCopierPanel.cs | Validate-only (no change) | 13 (JS-013 FINDING) | 13 (unchanged) | 0 |

---

## 8. Deferred Items

### Items Being Closed by B177

| ID | Block | Priority | Previous Status | Closed By | New Status |
|----|-------|----------|-----------------|-----------|------------|
| DW-B176-ATR-TEXTBOX-BROKEN | B176 | P0 | CLOSED by B176-1 (pipeline), but Director SIM found still broken | B177-1 verify pass | **TRULY CLOSED -- B177-1 VERIFY_PASS** |
| DW-B176-SIM | B176 | P1 | OPEN -- SIM pending post-B176 | Director SIM confirmed post-B176-FIX | **CLOSED -- Director SIM CONFIRMED** |

Note on DW-B176-ATR-TEXTBOX-BROKEN: The B176 pipeline marked this CLOSED after VERIFY_PASS,
but VERIFY_PASS in B176 validated the pipeline source (B176's filtered-guard approach with
CYC=5), not the final working source (B176-FIX with CYC=13 and manual injection). B177-1
re-validates against the final source and is the definitive close record.

### New Deferred Item Introduced by B177

| ID | Block | Priority | Description | Status |
|----|-------|----------|-------------|--------|
| DW-B177-CYC | B177 | P2 | `OnAtrBoxPreviewKeyDown` (TradeCopierPanel.cs lines 3318-3344) has CYC=13, which exceeds the JS-013 limit of 8 by 5 points. B177 is validate-only and cannot modify source. Reduction approach: extract the char-injection body (lines 3334-3343) into a `private void InjectAtrChar(char c)` helper method. This reduces the outer method to CYC≤8 and the helper to CYC≤3. Requires ptt-sync.ps1 run post-edit. | **OPEN -- carried forward to next block** |

---

## 9. Scope Boundary (Explicit)

| File | Action |
|------|--------|
| `src/PropTraderTools/TradeCopierPanel.cs` lines 3309-3344 | READ ONLY -- no edits |
| `src/PropTraderTools/AtrCalc.cs` | DO NOT TOUCH (B175 regression guard) |
| `src/PropTraderTools/CopyEngineTests.cs` | DO NOT TOUCH |
| `src/PropTraderTools/CopyEngine.cs` | DO NOT TOUCH |
| All other files | Unchanged |

---

## 10. Component Summary

| Component | Class | Method | File | Lines |
|-----------|-------|--------|------|-------|
| ATR% TextBox key handler | TradeCopierPanel (partial) | OnAtrBoxPreviewKeyDown | TradeCopierPanel.cs | 3318-3344 |

**NT8 API used**: None (WPF primitives only -- KeyEventArgs, TextBox properties).
**Threading**: UI thread only. No Dispatcher.InvokeAsync needed. No background thread access.
**Data flow**: PreviewKeyDown event -> guard chain -> (pass-through OR block-only OR block+inject) -> TextBox state updated in-place.

---

*Produced by ptt-architect -- Phase 1 (Cycle 2 -- REVIEW_FAIL correction)*
*Block: B177 | Violation fixed: VIOLATION-01 (JS-013 CYC=13, previously miscounted as CYC=8)*
*Deferred: DW-B177-CYC added (CYC reduction to ≤8 in future block)*
