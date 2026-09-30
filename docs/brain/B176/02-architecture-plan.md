# B176 Architecture Plan
# Author: ptt-architect (Phase 1)
# Status: PLAN_COMPLETE
# Date: 2025-01-29

---

## 1. LANE-SPLIT GATE RESULT

**LANE-SPLIT GATE RESULT: SINGLE-PIPELINE**

Gate answers:

| Question | Answer | Rationale |
|----------|--------|-----------|
| Q1. Same method or within 50 lines? | NO | B176-1 is TradeCopierPanel.cs ~line 3309. B176-2 is CopyEngine.cs line 3202. Different files. |
| Q2. Fix B design depends on Fix A final design? | NO | B176-2 is a comment-only change in CopyEngine.cs. Zero dependency on B176-1 logic. |
| Q3. Each fix has standalone value if the other is blocked? | YES | B176-1 alone restores TextBox input. B176-2 alone corrects documentation accuracy. |
| Q4. Each fix has an independent SIM verification path? | YES | B176-1: Director types into ATR% box (manual SIM). B176-2: grep check ("line 5507" absent). "No SIM needed" does NOT disqualify -- grep verification is a valid independent path. |

**Gate Decision**: Despite Q1=NO, Q2=NO, Q3=YES, Q4=YES, the write-sets are in **different files** with zero merge conflict. However, this is a simple two-ticket sequential block. No compelling reason for parallel lanes. **SINGLE-PIPELINE.**

---

## 2. Problem Analysis

### 2a. DW-B176-ATR-TEXTBOX-BROKEN (P0)

**Root cause: WPF event routing + architectural inversion**

WPF input events travel in two phases:
1. **Tunnel phase** (Preview*): Root -> Leaf. Fires on ancestors first, then the target element.
2. **Bubble phase** (non-Preview): Leaf -> Root. Fires on the target element first, then ancestors.

When `e.Handled = true` is set during a tunneling PreviewKeyDown event, the system suppresses:
- The corresponding bubbling KeyDown event at the target (Backspace, Delete, arrow keys)
- **In NT8's embedded chart host context**: also suppresses PreviewTextInput/TextInput processing

NT8's instrument-search interceptor is installed at the `ChartControl` ancestor level. Because the tunnel fires **ancestor-first**, NT8's interceptor runs **before** our leaf-level `OnAtrBoxPreviewKeyDown` handler. This means:

- The unconditional `e.Handled = true` in B175-2 **never** stopped NT8 from reading the key (NT8 already processed it in the tunnel before we set Handled)
- But it **did** suppress the downstream KeyDown at the TextBox, breaking Backspace, Delete, and arrow keys
- NT8's embedded chart host also interprets the Handled flag for TextInput processing, which suppresses digit/character input reaching the TextBox's text buffer

**The B175-2 fix was architecturally inverted**: it attempted to block NT8 by marking events handled in the wrong phase order, while simultaneously destroying the TextBox's own keyboard processing.

**Correct architecture**: Only mark `e.Handled = true` for unmodified alpha/digit keys. These are the ONLY keys NT8's search interceptor reads (it is looking for instrument symbol characters). Editing keys (Backspace, Delete, Home, End, arrow keys) and modifier combos (Ctrl+A, Ctrl+C, etc.) must pass through unmarked so they reach the TextBox's KeyDown and TextInput handlers.

**Note on printable characters**: In standard WPF (outside NT8's chart host), printable characters flow through the independent `PreviewTextInput` -> `TextInput` event pair, which is NOT suppressed by PreviewKeyDown's `e.Handled = true`. However, inside NT8's embedded chart host, the host's input processing intercepts before TextInput is fired, making the PreviewKeyDown guard essential for digit routing in that context.

**Code evidence** (verified at file read time):

```
src/PropTraderTools/TradeCopierPanel.cs line 3309-3316 (BEFORE):
    private void OnAtrBoxPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_atrFractionBox != null && _atrFractionBox.IsKeyboardFocused)
            e.Handled = true;   // <-- unconditional: kills Backspace, Delete, arrows
    }
```

**Subscription line** (line 3213, NOT touched by this block):
```
    _atrFractionBox.PreviewKeyDown += OnAtrBoxPreviewKeyDown;
```

### 2b. DW-B159-K5 (P3)

`CopyEngine.cs` line 3202 contains a stale cross-reference comment:
```
// Signal 1 (HasOpenPosition, line 5507): position Quantity > 0 (normal case, B159).
```

The `HasOpenPosition(Account, Instrument)` method definition is at **line 5526** (verified via grep at plan-write time). The comment was accurate when B159 was written but drifted as lines were inserted by subsequent blocks (B160-B175). No executable logic is affected.

---

## 3. Fix Design

### B176-1 -- OnAtrBoxPreviewKeyDown replacement

**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Lines**: 3309-3316 (method comment + body only -- subscription at 3213 NOT touched)
**Mechanism**: `apply_diff`

**BEFORE** (lines 3309-3316):
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

**AFTER**:
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

**CYC analysis for AFTER**:
- Branch 1: `null || !IsKeyboardFocused` early-exit guard
- Branch 2: early return path
- Branch 3: isAlpha upper-bound check (`e.Key <= Key.Z`)
- Branch 4: isDigit compound check (D0..D9 or NumPad0..NumPad9)
- Branch 5: `noMod` guard in final if
- **CYC = 5** (well within <= 8 target)

**Threading**: Event handler fires on WPF UI thread only. No cross-thread state. `_atrFractionBox` null check handles disposal race.

**e.Handled = true scope**: Exclusively inside `(isAlpha || isDigit) && noMod` guard. The unconditional form is completely absent from the new code.

### B176-2 -- DW-B159-K5 comment fix

**File**: `src/PropTraderTools/CopyEngine.cs`
**Line**: 3202
**Mechanism**: `apply_diff`

**BEFORE** (line 3202):
```csharp
        // Signal 1 (HasOpenPosition, line 5507): position Quantity > 0 (normal case, B159).
```

**AFTER** (line 3202):
```csharp
        // Signal 1 (HasOpenPosition, line 5526): position Quantity > 0 (normal case, B159).
```

**Verification**: `HasOpenPosition(Account acc, Instrument instrument)` is confirmed at line 5526 (grep verified at plan-write time). No executable lines changed. CYC delta = 0.

---

## 4. CYC Table

| Ticket | File | Method | CYC Before | CYC After | Delta | Compliant? |
|--------|------|--------|------------|-----------|-------|------------|
| B176-1 | TradeCopierPanel.cs | OnAtrBoxPreviewKeyDown | 2 | 5 | +3 | YES (<=8) |
| B176-2 | CopyEngine.cs | (comment only) | 0 | 0 | 0 | YES |

---

## 5. Unchanged Scope (explicit boundary)

The following are explicitly **NOT touched** by this block:

| Item | File | Line(s) | Reason |
|------|------|---------|--------|
| `_atrFractionBox.PreviewKeyDown += OnAtrBoxPreviewKeyDown` | TradeCopierPanel.cs | 3213 | Subscription is correct; method body is the fix target |
| `AtrCalc.cs` | AtrCalc.cs | all | B175-1 Wilder's ATR -- regression guard |
| `CopyEngineTests.cs` | CopyEngineTests.cs | all | No test changes required for these tickets |
| All other CopyEngine.cs logic | CopyEngine.cs | all except 3202 | Only the stale comment is touched |
| `TradeCopierWindow.cs` | TradeCopierWindow.cs | all | Not in scope |
| `TradeCopierAddOn.cs` | TradeCopierAddOn.cs | all | Not in scope |
| `FeatureFlags.cs` | FeatureFlags.cs | all | Not in scope |

---

## 6. Jane Street Compliance Summary

| Rule | Check | Result |
|------|-------|--------|
| JS-021: No lock() | New code contains no lock() statements | PASS |
| ASCII-only | All changed string literals are ASCII-only | PASS |
| No DateTime.Now | No date/time usage introduced | PASS |
| No FontFamily | No font usage introduced | PASS |
| CYC <= 8 | OnAtrBoxPreviewKeyDown = 5 after fix | PASS |
| apply_diff only (no write_file on .cs) | Engineer must use apply_diff for both tickets | REQUIRED |
| ptt-sync.ps1 run | Run ONCE after B176-2 completes | REQUIRED |
| AtrCalc.cs not modified | B175 regression guard | REQUIRED |
| e.Handled = true only inside guard | Unconditional form absent from new code | PASS |
| No Unicode/curly quotes | No non-ASCII characters in changed lines | PASS |

---

## 7. Ticket Order and ptt-sync.ps1 Run Plan

**Order**: B176-1 first (TradeCopierPanel.cs), B176-2 second (CopyEngine.cs).

**ptt-sync.ps1**: Runs **ONCE** after B176-2 completes (after both tickets are applied).

**Rationale for order**: B176-1 is the higher-priority (P0) fix. B176-2 is a trivial comment change. Sequential is safe -- no interdependency.

**ptt-sync.ps1 command** (run from workspace root after B176-2):
```powershell
powershell -File .\scripts\ptt-sync.ps1
```
Expected output: 18 linked, 0 errors (same as B175 terminal state).

---

## 8. 7-Scan Checklist (pre-populated for tickets)

Engineer must run and record all 7 scans at B176-2 completion (terminal state):

| Scan | Command / Check | Pass Criterion |
|------|-----------------|----------------|
| SCAN-01 | `grep -c "lock(" src/PropTraderTools/TradeCopierPanel.cs` | 0 |
| SCAN-02 | Verify `e.Handled = true` in `OnAtrBoxPreviewKeyDown` is ONLY inside `(isAlpha \|\| isDigit) && noMod` guard | No unconditional form present |
| SCAN-03 | CYC check: `OnAtrBoxPreviewKeyDown` branch count | Exactly 5 |
| SCAN-04 | ASCII-only: no non-ASCII chars in changed lines (lines 3309-3328 of TradeCopierPanel.cs, line 3202 of CopyEngine.cs) | 0 non-ASCII chars |
| SCAN-05 | `powershell -File .\scripts\ptt-sync.ps1` output | 18 linked, 0 errors |
| SCAN-06 | `AtrCalc.cs` not modified: `git diff src/PropTraderTools/AtrCalc.cs` | Empty diff (no changes) |
| SCAN-07 | `grep "line 5507" src/PropTraderTools/CopyEngine.cs` | 0 matches (stale ref absent) |

---

## 9. Tickets

### Ticket B176-1

**Epic**: B176  
**Defect**: DW-B176-ATR-TEXTBOX-BROKEN (P0)  
**File**: `src/PropTraderTools/TradeCopierPanel.cs`  
**Mechanism**: `apply_diff` ONLY (no write_file)  

**Method signature** (unchanged -- same name/params):
```csharp
private void OnAtrBoxPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
```

**Change**: Replace lines 3309-3316 (comment block + method body) with the key-filtered implementation shown in Section 3.

**JS rules applicable**:
- JS-021: No lock()
- ASCII-only in all string literals
- CYC of modified method must be <= 8 (target: exactly 5)
- UI thread only -- no Dispatcher.InvokeAsync needed (event handler is already on UI thread)

**xUnit tests**: None required. Verification is manual SIM (Director types into ATR% box, observes digit input, Backspace, Delete, arrows all work; no instrument-search dialog appears). WPF event handler logic is not unit-testable via xUnit in this project's test harness.

**Verification criteria**:
1. Digit keys (0-9) typed into ATR% box appear in the box
2. Backspace deletes the last character
3. Delete key works
4. Arrow keys move cursor within box
5. No instrument-search dialog appears when typing a letter (e.g. 'E' for ES) while box is focused
6. Ctrl+A selects all text in box

**Scans to run after this ticket**: SCAN-01, SCAN-02, SCAN-03, SCAN-04 (do not run SCAN-05/07 until B176-2 is also complete).

---

### Ticket B176-2

**Epic**: B176  
**Defect**: DW-B159-K5 (P3)  
**File**: `src/PropTraderTools/CopyEngine.cs`  
**Mechanism**: `apply_diff` ONLY (no write_file)  

**Change**: Line 3202 -- replace `line 5507` with `line 5526` in the comment string.

**JS rules applicable**:
- ASCII-only (comment text only -- no change needed, already ASCII)
- No executable change: CYC unchanged, no new branches

**xUnit tests**: None. Comment-only change. No test assertions possible or needed.

**Verification criteria**:
1. `grep "line 5507" src/PropTraderTools/CopyEngine.cs` returns 0 matches
2. `grep "line 5526" src/PropTraderTools/CopyEngine.cs` shows the corrected comment at line 3202
3. `dotnet build` produces 0 errors (comment change cannot introduce errors, but run as standard gate)

**Scans to run after this ticket** (terminal state -- all 7):
Run full 7-scan checklist as documented in Section 8.
Run `powershell -File .\scripts\ptt-sync.ps1` (SCAN-05).

---

## 10. Deferred Backlog Updates

**Items CLOSED by B176**:

| ID | Description | Closed By |
|----|-------------|-----------|
| DW-B176-ATR-TEXTBOX-BROKEN | ATR% TextBox non-functional after B175-2 (unconditional e.Handled) | B176-1 |
| DW-B159-K5 | Stale line comment in CopyEngine.cs (5507 -> 5526) | B176-2 |

**New items opened by B176**:

| ID | Description | Priority |
|----|-------------|----------|
| DW-B176-SIM | SIM GATE: Director clicks ATR% box, types digits and editing keys -- verify digits appear in box, Backspace/Delete/arrows work, no instrument-search dialog. Also verify B175 ATR formula correctness is unaffected (closes DW-B175-SIM if both pass in same run). | P1 |

**Items remaining OPEN** (unchanged from B175/06-deferred-backlog.md):
- DW-B175-SIM (P1): Superseded/merged into DW-B176-SIM above
- DW-B170-SIM (P0): Stop-drag sync SIM gate
- DW-B169-K1, K2 (P0): 3-stop / single-stop drag SIM
- B167-DEFER-02 (P1): fo=NULL follower ATM brackets
- B167-DEFER-01 (P2): ECA-1 double-position race
- B167-DEFER-03, 04 (P1): Combined B161-B166 and B167 SIM gates
- DW-B159-K2 (P2): 14 xUnit tests
- DW-B159-K3 (P2): Attempt cap hardcoded at 3
- DW-B159-K4 (P2): intendedBePrice sourcing

---

## 11. Component Summary

| Component | Class | File | Change Type |
|-----------|-------|------|-------------|
| ATR Widget keyboard handler | `TradeCopierPanel.AtrWidget` | `src/PropTraderTools/TradeCopierPanel.cs` | Method body + comment replacement |
| BE Guard comment | `CopyEngine` | `src/PropTraderTools/CopyEngine.cs` | Comment string only |

**NinjaTrader 8 API used**: `System.Windows.Input.Key` enumeration (WPF, not NT8-specific). `System.Windows.Input.ModifierKeys` enumeration. `System.Windows.Input.KeyboardDevice.Modifiers` property. All are standard WPF types available in NT8's WPF host context.

No NT8-specific API (`Account.All`, `CreateOrder`, `AtmStrategy*`) is introduced or modified by this block.
