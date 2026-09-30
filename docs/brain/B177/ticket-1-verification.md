# B177-1 VERIFICATION REPORT (Layer 3)

**Block**: B177 -- DW-B176-TEXTBOX-FIX-PIPELINE
**Ticket**: B177-1 -- Validate B176-FIX Repair in TradeCopierPanel.cs lines 3309-3344
**Phase**: 4b -- Verifier (Independent Re-Run)
**Verifier**: PTT Verifier (Layer 3)
**Source file read**: src/PropTraderTools/TradeCopierPanel.cs lines 3309-3344 (READ ONLY)
**Status**: VERIFY_PASS

---

## VERIFICATION SCOPE

This report independently re-verifies the B176-FIX repair (lines 3309-3344 of TradeCopierPanel.cs)
by re-running all 11 checks from source. The engineer's Layer 2 self-report (ticket-1-completion.md)
is NOT trusted; every check is re-derived from source or a fresh shell command.

---

## SOURCE EXCERPT (lines 3309-3344 -- read directly)

```csharp
3309: // B176-FIX: PreviewKeyDown on _atrFractionBox.
3310: // NT8's chart-level search interceptor opens a search dialog for alpha/digit/period
3311: // keys. e.Handled=true in PreviewKeyDown suppresses it -- BUT in NT8's embedded WPF
3312: // host e.Handled=true also suppresses TextInput, so we must manually inject the
3313: // printable character into the TextBox at the caret position.
3314: // Editing keys (Backspace, Delete, arrows, Ctrl/Shift) are NOT in our intercept list
3315: // and reach the TextBox's own KeyDown handler unchanged.
3316: // CYC=8: null/focus(1)+noMod(2)+passThrough(3)+isAlpha(4)+isPeriod(5)+isDigitD(6)
3317: //        +selRemove(7)+base(8). JS-021: no lock. UI thread only. ASCII-only.
3318: private void OnAtrBoxPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
3319: {
3320:     if (_atrFractionBox == null || !_atrFractionBox.IsKeyboardFocused)
3321:         return;
3322:     bool noMod    = e.KeyboardDevice.Modifiers == System.Windows.Input.ModifierKeys.None;
3323:     if (!noMod)
3324:         return;
3325:     bool isAlpha  = e.Key >= System.Windows.Input.Key.A && e.Key <= System.Windows.Input.Key.Z;
3326:     bool isDigitD = e.Key >= System.Windows.Input.Key.D0 && e.Key <= System.Windows.Input.Key.D9;
3327:     bool isDigitN = e.Key >= System.Windows.Input.Key.NumPad0 && e.Key <= System.Windows.Input.Key.NumPad9;
3328:     bool isPeriod = e.Key == System.Windows.Input.Key.OemPeriod || e.Key == System.Windows.Input.Key.Decimal;
3329:     if (!isAlpha && !isDigitD && !isDigitN && !isPeriod)
3330:         return; // Backspace, Delete, arrows: not our keys -- pass through unchanged
3331:     e.Handled = true; // block NT8 search interceptor
3332:     if (isAlpha)
3333:         return; // alpha: blocked from NT8, not valid in numeric field, no inject
3334:     // Inject printable char at caret (digits + period only)
3335:     char c = isPeriod ? '.'
3336:            : isDigitD ? (char)('0' + (e.Key - System.Windows.Input.Key.D0))
3337:            : (char)('0' + (e.Key - System.Windows.Input.Key.NumPad0));
3338:     int sel = _atrFractionBox.SelectionStart;
3339:     int len = _atrFractionBox.SelectionLength;
3340:     string t = _atrFractionBox.Text ?? string.Empty;
3341:     if (len > 0) t = t.Remove(sel, len);
3342:     _atrFractionBox.Text = t.Insert(sel, c.ToString());
3343:     _atrFractionBox.CaretIndex = sel + 1;
3344: }
```

---

## 11 VERIFICATION CHECKS

### V01 -- All 4 bool declarations present

**Expected**: isDigitD, isDigitN, isPeriod, isAlpha all declared in lines 3309-3344.

**Layer 3 finding (from source read)**:
- Line 3325: `bool isAlpha  = e.Key >= ... Key.A && e.Key <= ... Key.Z;` -- PRESENT
- Line 3326: `bool isDigitD = e.Key >= ... Key.D0 && e.Key <= ... Key.D9;` -- PRESENT
- Line 3327: `bool isDigitN = e.Key >= ... Key.NumPad0 && e.Key <= ... Key.NumPad9;` -- PRESENT
- Line 3328: `bool isPeriod = e.Key == ... Key.OemPeriod || e.Key == ... Key.Decimal;` -- PRESENT

**Result**: PASS

---

### V02 -- OemPeriod AND Decimal both present in isPeriod

**Expected**: Both Key.OemPeriod and Key.Decimal in the isPeriod boolean expression.

**Layer 3 finding (from source read)**:
Line 3328: `bool isPeriod = e.Key == System.Windows.Input.Key.OemPeriod || e.Key == System.Windows.Input.Key.Decimal;`
Both `Key.OemPeriod` and `Key.Decimal` confirmed present on the same line, joined by `||`.

**Result**: PASS

---

### V03 -- e.Handled = true exactly once, AFTER pass-through guard, BEFORE injection

**Expected**: Exactly 1 occurrence of `e.Handled = true` in lines 3309-3344; positioned after the
pass-through guard at 3329-3330 and before the injection block at 3335-3343.

**Layer 3 finding (from source read)**:
- Occurrence count: 1 (line 3331 only)
- Preceding guards: null/focus (3320), noMod (3323), pass-through (3329-3330) -- all three are return-early guards that execute BEFORE line 3331
- Following injection: char c (3335), SelectionStart (3338), SelectionLength (3339), Text.Insert (3342), CaretIndex (3343) -- all AFTER line 3331

Positional flow confirmed: guard chain -> e.Handled=true (3331) -> alpha gate (3332) -> injection block (3335-3343)

**Result**: PASS

---

### V04 -- Injection block present: SelectionStart, SelectionLength, Text.Insert, CaretIndex

**Expected**: All 4 identifiers present in lines 3309-3344.

**Layer 3 finding (from source read)**:
- `SelectionStart` at line 3338: `int sel = _atrFractionBox.SelectionStart;` -- PRESENT
- `SelectionLength` at line 3339: `int len = _atrFractionBox.SelectionLength;` -- PRESENT
- `Text.Insert` (via `t.Insert`) at line 3342: `_atrFractionBox.Text = t.Insert(sel, c.ToString());` -- PRESENT
- `CaretIndex` at line 3343: `_atrFractionBox.CaretIndex = sel + 1;` -- PRESENT

**Result**: PASS

---

### V05 -- if (len > 0) t.Remove present (selection replacement guard)

**Expected**: Conditional removal guard `if (len > 0) t = t.Remove(sel, len)` present.

**Layer 3 finding (from source read)**:
Line 3341: `if (len > 0) t = t.Remove(sel, len);` -- exact pattern confirmed verbatim.

**Result**: PASS

---

### V06 -- No unconditional e.Handled = true

**Expected**: `e.Handled = true` must NOT appear before the intercept guard chain (would block all keys unconditionally).

**Layer 3 finding (from source read)**:
Execution order in method:
  3320-3321: null/focus guard (early return if unattended)
  3322-3324: noMod declaration + early return
  3325-3328: bool declarations (no branching yet)
  3329-3330: pass-through guard (early return for non-intercept keys)
  3331:       e.Handled = true   <-- FIRST AND ONLY occurrence

The `e.Handled = true` at line 3331 is reached ONLY after all three early-return guards.
Any Backspace, Delete, arrow, or modified key exits at lines 3321, 3324, or 3330 BEFORE reaching line 3331.
There is no unconditional assignment at the top of the method.

**Result**: PASS

---

### V07 -- CYC = 13 (independent McCabe count)

**Expected**: Independent count yields CYC=13. Source comment at 3316-3317 claims CYC=8 -- that comment is WRONG and is not authoritative.

**Layer 3 independent count**:

| # | Line | Construct | +CYC | Running Total |
|---|------|-----------|------|---------------|
| base | -- | base count | +1 | 1 |
| 1 | 3320 | `if (_atrFractionBox == null \|\| ...)` -- `if` keyword | +1 | 2 |
| 2 | 3320 | `\|\|` logical-or in null/focus compound | +1 | 3 |
| 3 | 3323 | `if (!noMod)` | +1 | 4 |
| 4 | 3329 | `if (!isAlpha && !isDigitD && !isDigitN && !isPeriod)` -- `if` keyword | +1 | 5 |
| 5 | 3329 | first `&&` (!isAlpha && !isDigitD) | +1 | 6 |
| 6 | 3329 | second `&&` (!isDigitD && !isDigitN) | +1 | 7 |
| 7 | 3329 | third `&&` (!isDigitN && !isPeriod) | +1 | 8 |
| 8 | 3332 | `if (isAlpha)` | +1 | 9 |
| 9 | 3335 | `?:` -- isPeriod ternary (first branch) | +1 | 10 |
| 10 | 3336 | `?:` -- isDigitD ternary (second branch) | +1 | 11 |
| 11 | 3340 | `??` -- null-coalesce on `.Text` | +1 | 12 |
| 12 | 3341 | `if (len > 0)` -- selection removal | +1 | 13 |

**Total: 12 decision points + 1 base = CYC = 13**

Source comment at lines 3316-3317 states "CYC=8" -- this is INCORRECT.
The comment omits: the `||` at line 3320, the three `&&` at line 3329, the `??` at line 3340.
Per JS-013, each `if`, `||`, `&&`, `?:`, `??` = +1. Authoritative count = **13**.

JS-013 limit is 8. CYC=13 exceeds it by 5 points.
This is a known compliance finding carried as **DW-B177-CYC (P2)**.
Per architecture plan Section 4 and ticket spec, this finding does NOT block VERIFY_PASS for B177-1
because: source is already deployed, Director SIM confirmed working, B177 is validate-only (no source change permitted), and reduction is deferred to a future block.

**Result**: PASS (JS-013 FINDING acknowledged -- DW-B177-CYC OPEN)

---

### V08 -- No lock() in lines 3309-3344

**Command run (Layer 3 independent)**:
```powershell
Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "lock\(" -SimpleMatch | Where-Object { $_.LineNumber -ge 3309 -and $_.LineNumber -le 3344 }
```

**Output**: (no output -- zero matches)

No `lock(` occurrences in lines 3309-3344. JS-021 COMPLIANT.

**Result**: PASS

---

### V09 -- No non-ASCII in lines 3309-3344

**Command run (Layer 3 independent)**:
```powershell
$content = Get-Content src/PropTraderTools/TradeCopierPanel.cs; $lines = $content[3308..3343]; $hits = $lines | Where-Object { $_ -match '[^\x00-\x7F]' }; if ($hits) { $hits } else { "ZERO non-ASCII" }
```

**Output**:
```
ZERO non-ASCII
```

All characters in lines 3309-3344 are in the ASCII range (0x00-0x7F).

**Result**: PASS

---

### V10 -- AtrCalc.cs Untracked (B175 regression guard)

**Command run (Layer 3 independent)**:
```powershell
git status src/PropTraderTools/AtrCalc.cs
```

**Output**:
```
On branch main
Your branch and 'origin/main' have diverged,
and have 40 and 839 different commits each, respectively.

Untracked files:
  (use "git add <file>..." to include in what will be committed)
        src/PropTraderTools/AtrCalc.cs

nothing added to commit but untracked files present (use "git add" to track)
```

AtrCalc.cs status is `??` (Untracked) -- NOT Modified (M) and NOT Deleted (D).
B175 regression guard is intact.

**AtrCalc.cs: Untracked (not modified)**

**Result**: PASS

---

### V11 -- ptt-sync.ps1: 18 linked, 0 errors

**Command run (Layer 3 independent)**:
```powershell
powershell -File .\scripts\ptt-sync.ps1
```

**Exact output**:
```
LINKED  AtrCalc.cs
LINKED  CopyEngine.cs
LINKED  FeatureFlags.cs
LINKED  LicenseClient.cs
LINKED  TradeCopierAddOn.cs
LINKED  TradeCopierPanel.cs
LINKED  TradeCopierWindow.cs
LINKED  Core\PttContracts.cs
LINKED  Features\PttBreakEven.cs
LINKED  Features\PttBreakEvenSwap.cs
LINKED  Features\PttCancel.cs
LINKED  Features\PttCopier.cs
LINKED  Features\PttFlatten.cs
LINKED  Features\PttFollowerStrategy.cs
LINKED  Features\PttGlobalBreakEven.cs
LINKED  Features\PttGlobalQuickExit.cs
LINKED  Features\PttQuickExit.cs
LINKED  Features\PttTrim.cs

ptt-sync complete: 18 linked, 0 errors
```

18 files linked. 0 errors.

**ptt-sync.ps1: 18 linked, 0 errors**

**Result**: PASS

---

## VERIFICATION SUMMARY TABLE

| Check | Description | Layer 3 Result | Status |
|-------|-------------|----------------|--------|
| V01 | All 4 bool declarations present | isAlpha(3325), isDigitD(3326), isDigitN(3327), isPeriod(3328) confirmed | PASS |
| V02 | OemPeriod AND Decimal in isPeriod | Both on line 3328, joined by `\|\|` | PASS |
| V03 | e.Handled=true exactly once, after guard, before inject | Line 3331 only; 3 guards precede; inject follows | PASS |
| V04 | Injection block: SelectionStart, SelectionLength, Text.Insert, CaretIndex | All 4 at lines 3338, 3339, 3342, 3343 | PASS |
| V05 | if (len>0) t.Remove guard present | Line 3341 verbatim | PASS |
| V06 | No unconditional e.Handled=true | Confirmed: 3 early-return guards before line 3331 | PASS |
| V07 | CYC=13 (independent count) | 12 decision points + base = 13; source comment (CYC=8) is wrong | PASS (JS-013 FINDING) |
| V08 | No lock() in lines 3309-3344 | Shell scan: 0 matches | PASS |
| V09 | No non-ASCII in lines 3309-3344 | Shell scan: ZERO non-ASCII | PASS |
| V10 | AtrCalc.cs Untracked | git status: `??` confirmed | PASS |
| V11 | ptt-sync.ps1: 18 linked, 0 errors | Shell run: exact output matches | PASS |

---

## LAYER 2 vs LAYER 3 CROSS-CHECK

The engineer's Layer 2 report (ticket-1-completion.md) is compared against my independent Layer 3 results:

| Check | Layer 2 Claim | Layer 3 Finding | Match? |
|-------|---------------|-----------------|--------|
| V01 (bool declarations) | All 4 present (lines 3325-3328) | Confirmed from source | YES |
| V02 (OemPeriod + Decimal) | Both on line 3328 | Confirmed from source | YES |
| V03 (e.Handled once, positioned correctly) | Exactly 1 at line 3331, after guards, before inject | Confirmed from source | YES |
| V04 (injection block identifiers) | All 4 at lines 3338, 3339, 3342, 3343 | Confirmed from source | YES |
| V05 (len>0 t.Remove) | Line 3341 | Confirmed from source | YES |
| V06 (no unconditional e.Handled) | Confirmed -- 3 guards precede line 3331 | Confirmed from source | YES |
| V07 (CYC) | CYC=13 per independent count; source comment is wrong | CYC=13 confirmed by independent count | YES |
| V08 (no lock) | SCAN-01: zero matches | Shell scan: zero matches | YES |
| V09 (no non-ASCII) | SCAN-03: ZERO non-ASCII | Shell scan: ZERO non-ASCII | YES |
| V10 (AtrCalc.cs Untracked) | `??` confirmed | git status: Untracked confirmed | YES |
| V11 (ptt-sync.ps1) | 18 linked, 0 errors | Shell run: 18 linked, 0 errors | YES |

**Discrepancies: ZERO**

All 11 Layer 3 checks match the engineer's Layer 2 self-report exactly.

---

## DNA RULE COMPLIANCE (Jane Street Rules Catalog)

| Rule | Finding | Status |
|------|---------|--------|
| JS-021: No lock() | Zero lock() in lines 3309-3344 (V08 PASS) | PASS |
| JS-013: CYC <= 8 | CYC=13 (FINDING) -- exceeds limit by 5. Deferred as DW-B177-CYC. | FINDING (non-blocking for B177-1) |
| No async/await in NT8 lifecycle methods | No async/await in lines 3309-3344 | PASS |
| No FontFamily= (NT8 constraint) | Not present in lines 3309-3344 | PASS |
| No #RRGGBB hex colors (SCAN-04 equivalent) | Not present in lines 3309-3344 | PASS |
| No DateTime.Now (use UtcNow) | Not present in lines 3309-3344 | PASS |
| No non-ASCII (ASCII-only compliance) | ZERO non-ASCII confirmed (V09 PASS) | PASS |
| No throw in dispatch/gate methods | Not present in lines 3309-3344 | PASS |
| No sealed on TradeCopierWindow | Not applicable to this method | N/A |
| e.Handled = true exactly once, after guard | Confirmed (V03 PASS, V06 PASS) | PASS |

---

## JS-013 COMPLIANCE FINDING ACKNOWLEDGMENT

**Finding ID**: DW-B177-CYC
**Priority**: P2
**File**: src/PropTraderTools/TradeCopierPanel.cs
**Method**: OnAtrBoxPreviewKeyDown (lines 3318-3344)
**CYC measured**: 13 (Layer 3 independent count)
**CYC limit (JS-013)**: 8
**Excess**: 5 points

Source comment at lines 3316-3317 states "CYC=8". This is INCORRECT.
Authoritative count per JS-013 methodology = 13.

B177 is validate-only. No source change is permitted in this block.
Future reduction plan: extract lines 3334-3343 (char-injection body) into
`private void InjectAtrChar(char c)` helper. This reduces outer method to CYC<=8
and helper to CYC<=3. Requires ptt-sync.ps1 run post-edit. Not in scope for B177.

Status: OPEN -- carried to next block.

This finding does NOT block VERIFY_PASS for B177-1 per the approved architecture plan
(Section 4, JS-013 Compliance Finding Rationale) and ticket spec gate logic.

---

## EXPLICIT CONFIRMATIONS

**No .cs files were modified in B177-1.**

Source files read (READ ONLY): src/PropTraderTools/TradeCopierPanel.cs (lines 3309-3344)
Source files written: NONE
Source files deleted: NONE

**AtrCalc.cs: Untracked (not modified)**

git status confirms: `?? src/PropTraderTools/AtrCalc.cs`
Status is Untracked, not Modified (M) and not Deleted (D). B175 regression guard intact.

**ptt-sync.ps1: 18 linked, 0 errors**

powershell -File .\scripts\ptt-sync.ps1 output: `ptt-sync complete: 18 linked, 0 errors`

---

## DEFERRED ITEMS

### Closed by B177-1 VERIFY_PASS

| ID | Priority | Description | Status |
|----|----------|-------------|--------|
| DW-B176-TEXTBOX-FIX | P0 | ATR% TextBox digit/period input broken in NT8 -- search dialog opened instead of typing. B176-FIX applied manual caret injection. | CLOSED -- B177-1 VERIFY_PASS |
| DW-B176-SIM | P1 | Director SIM PASS confirming repair works in NT8 environment. | CLOSED -- Director SIM confirmed pre-B177 |

### Opened by B177-1 VERIFY_PASS

| ID | Priority | Description | Status |
|----|----------|-------------|--------|
| DW-B177-CYC | P2 | OnAtrBoxPreviewKeyDown CYC=13 exceeds JS-013 limit of 8 by 5 points. Extract InjectAtrChar(char c) helper to reduce. Requires ptt-sync.ps1 run post-edit. | OPEN -- carry forward |

---

## SUMMARY

B177-1 VERIFY_PASS

All 11 verification checks PASS. All required source elements confirmed in lines 3309-3344.
Zero Layer 2 vs Layer 3 discrepancies. ptt-sync.ps1: 18 linked, 0 errors.
AtrCalc.cs: Untracked (not modified). No .cs files were modified in B177-1.

The sole non-conformance is CYC=13 (JS-013 FINDING, DW-B177-CYC P2), which is a known
compliance finding. Per architecture plan and ticket gate logic, it does not block VERIFY_PASS
for this validate-only block.

DW-B176-TEXTBOX-FIX (P0) and DW-B176-SIM (P1) are formally closed by this verification pass.

---

VERIFY_PASS
