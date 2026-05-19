# Phase 7 Sprint 5 - T09: CreateTextBox CYC Reduction

**BUILD_TAG**: `1111.007-phase7-t9`  
**Ticket**: [Phase7-S5-T09] CreateTextBox (CYC=26 -> <20)  
**File**: `src/V12_002.UI.Panel.Helpers.cs`  
**Target Function**: `CreateTextBox` (lines 59-129)

## Objective

Extract `CreateTextBox` (CYC=26, LOC=71) into a thin residual dispatcher (CYC ≤19) plus ~3 sub-helpers. Pure refactor with ZERO visual or behavioral change to the V12 control panel.

## Scope

**In Scope:**
- Sub-helper extraction within `src/V12_002.UI.Panel.Helpers.cs`
- CYC reduction from 26 to <20
- Maintain identical TextBox styling across all 5+ caller sites

**Out of Scope:**
- Logic changes or behavioral modifications
- Modifying caller sites (signature LOCKED per D-D3 high fan-out)
- Changing brushes, fonts, margins, or visual properties

## Approach

### Current Complexity Analysis
- **Total CYC**: 26 (target: <20)
- **LOC**: 71 (including comments)
- **Complexity Sources**:
  - TextBox creation + styling: CYC ~2
  - PreviewKeyDown event handler: CYC ~22 (multiple key branches)
  - GotKeyboardFocus handler: CYC ~1

### Extraction Strategy

**1. CreateTextBoxBase** (CYC ≤2)
```csharp
private TextBox CreateTextBoxBase(string defaultText, double width)
```
- Creates TextBox with base styling (Background, Foreground, BorderBrush, etc.)
- Handles width assignment logic
- Returns unstyled TextBox ready for event handlers

**2. HandleTextBoxKeyInput** (CYC ≤11)
```csharp
private void HandleTextBoxKeyInput(TextBox textBox, KeyEventArgs e)
```
- Extracts the manual keyboard pipeline logic from PreviewKeyDown
- Handles all key processing branches (digits, numpad, backspace, delete, etc.)
- Manages caret positioning and text insertion

**3. ApplyTextBoxKeyboardHandlers** (CYC ≤1)
```csharp
private void ApplyTextBoxKeyboardHandlers(TextBox textBox)
```
- Attaches PreviewKeyDown and GotKeyboardFocus event handlers
- PreviewKeyDown delegates to HandleTextBoxKeyInput
- Maintains Phase 7 [KB-R1] manual text pipeline behavior

**4. Residual CreateTextBox** (CYC ≤3)
```csharp
private TextBox CreateTextBox(string defaultText, double width)
{
    var tb = CreateTextBoxBase(defaultText, width);
    ApplyTextBoxKeyboardHandlers(tb);
    return tb;
}
```

## Guardrails

### INV-8: Visual Consistency
- **INV-8.1**: All 5+ call sites receive identical TextBox styling
- **INV-8.2**: No per-call style object allocation (shared brush references)
- **INV-8.3**: `defaultText` and `width` parameters preserved
- **INV-8.4**: TextBox event-wiring preserved per call site

### V12 DNA Compliance
- **INV-1.1 - INV-1.5**: Cross-cutting V12 constraints maintained
- **ASCII-Only**: No Unicode in string literals
- **Signature Lock**: `CreateTextBox(string defaultText, double width)` frozen

### Performance
- UI construct-time code (runs once per `OnStateChangeRealtime`)
- No GC sensitivity requirements
- Shared static brush references maintained

## Verification

### Step 1: Pre-Extraction Baseline
```powershell
# Verify current state
powershell -File .\scripts\build_readiness.ps1
# Confirm CYC=26 for CreateTextBox
```

### Step 2: Implementation
- Extract sub-helpers in order: CreateTextBoxBase → HandleTextBoxKeyInput → ApplyTextBoxKeyboardHandlers
- Refactor CreateTextBox to thin dispatcher
- Maintain exact same styling and event behavior

### Step 3: Compilation Verification
```powershell
powershell -File .\scripts\build_readiness.ps1
# Verify zero build errors
```

### Step 4: CYC Verification
- Residual `CreateTextBox`: CYC ≤3
- `CreateTextBoxBase`: CYC ≤2  
- `HandleTextBoxKeyInput`: CYC ≤11
- `ApplyTextBoxKeyboardHandlers`: CYC ≤1
- `CreateTextBox` no longer appears in "CYC > 20 remaining"

### Step 5: F5 Visual Acceptance
**Criterion**: Open V12 control panel; visually verify `priceInput`, `beOffsetInput`, `trailDistInput`, `svT1Val`, etc. render with identical font/brush/margin/alignment to pre-Sprint baseline.

## Acceptance Criteria

1. ✅ Residual `CreateTextBox` measures CYC ≤19
2. ✅ All sub-helpers measure CYC ≤19 (LOC ≥15 modulo DEVIATION-T9-A)
3. ✅ `CreateTextBox` no longer appears in `CYC > 20 remaining`
4. ✅ All 5+ caller sites unchanged (signature preserved)
5. ✅ F5 visual diff: identical rendering to pre-extraction baseline
6. ✅ BUILD_TAG bumped to `1111.007-phase7-t9`
7. ✅ Zero compilation errors
8. ✅ Phase 7 [KB-R1] manual text pipeline behavior preserved

## Implementation Notes

- **DEVIATION-T9-A**: 52 LOC short of target — sub-helpers may be ~10-20 LOC
- **Event Handler Preservation**: Critical for NT8 chart keyboard hijack prevention
- **Brush Sharing**: Maintain references to `BgSlate`, `TextPrimary`, `BtnBorder`, `ConsolasFont`
- **Caret Management**: Preserve exact text insertion and selection behavior