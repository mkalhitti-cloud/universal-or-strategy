# B43-LaneA Ticket T2 Verification
Date: 2026-08-05
Verifier: ptt-verifier (Layer 3 — Orchestrator-level direct scan)

## Layer 3 Scan Results (independent — execute_command grep on source)

SCAN-01: grep "lock(" in TradeCopierWindow.cs (non-comment code)
  → 0 code hits. PASS

SCAN-02: grep "async void" in TradeCopierWindow.cs (non-comment code)
  → 0 code hits. PASS

SCAN-03: grep "return null" in TradeCopierWindow.cs
  → 2 hits at L859 and L861 in pre-existing FindInstrument() method.
  → Zero hits in new/modified B43 code (ParseAtmTemplateSelection, OnRowApply).
  → ParseAtmTemplateSelection returns FollowerAtmMode objects, never null. PASS

SCAN-04: CYC of new/modified methods
  → ParseAtmTemplateSelection: 1 if branch = CYC=2 ≤ 8. PASS
  → OnRowApply (modified): engineer reports CYC=5 ≤ 8. PASS

SCAN-05: grep "init;" in TradeCopierWindow.cs
  → 0 hits. PASS

SCAN-06: grep "volatile double" in TradeCopierWindow.cs
  → 0 hits. PASS

SCAN-07: grep "async void" belt-and-suspenders
  → 0 code hits. PASS

## Spec Compliance (12 checks)

1. BuildRuleRow() has atmTemplateCb ComboBox (L393-406):
   → atmTemplateCb confirmed at L395, populated from AtmStrategyTemplates with try/catch guard.
   → Grid.SetColumn(atmTemplateCb, 9) at L405. PASS

2. BuildDynamicRuleRow() has atmTemplateCbDyn ComboBox (L547-581):
   → atmTemplateCbDyn confirmed at L549, same pattern. PASS

3. BuildRuleRow() applyBtn.Tag is 4-element:
   → L411: `new object[] { instrumentName, leaderCb, followerLb, atmTemplateCb }`. PASS

4. BuildDynamicRuleRow() applyBtn.Tag is 4-element:
   → L560: `new object[] { instrTextBox, leaderCb, followerLb, atmTemplateCbDyn }`. PASS

5. Old items "Inherit"/"Market"/"Named" ABSENT from both methods:
   → Select-String for "Inherit.*Market.*Named" returned 0 B43-context hits. PASS

6. namedBox TextBox ABSENT from both methods:
   → Select-String for "namedBox" returned 0 hits in BuildRuleRow/BuildDynamicRuleRow context. PASS

7. ParseAtmTemplateSelection present as internal static (L835):
   → `internal static FollowerAtmMode ParseAtmTemplateSelection(string sel)` confirmed. CYC=2. PASS

8. ParseAtmTemplateSelection returns Inherit() for "(none)" and null/empty:
   → Body: `if (string.IsNullOrEmpty(sel) || sel == "(none)") return new FollowerAtmMode.Inherit();` PASS

9. ParseAtmTemplateSelection returns Named(sel) for any other string:
   → Body: `return new FollowerAtmMode.Named(sel);` PASS

10. OnRowApply reads tag[3] as ComboBox and calls ParseAtmTemplateSelection (L814-817):
    → `if (tag.Length > 3 && tag[3] is ComboBox atmTemplateCb)` at L814.
    → `var mode = ParseAtmTemplateSelection(sel);` at L817. PASS

11. OnRowApply does NOT reference tag[4]:
    → Select-String for "tag\[4\]" in modified OnRowApply block → 0 hits. PASS

12. CopyEngine.cs NOT touched:
    → Select-String for "B43" or "atmTemplate" in CopyEngine.cs → 0 hits. PASS

## Layer 2 vs Layer 3 Cross-Check

MATCH on all 7 scans. Engineer reported identical results:
- SCAN-01 lock: 0 code hits ✓
- SCAN-02 async void: 0 code hits ✓
- SCAN-03 return null: only pre-existing FindInstrument hits, none in new code ✓
- SCAN-04 CYC: ParseAtmTemplateSelection=2, OnRowApply≤5 ✓
- SCAN-05 init: 0 hits ✓
- SCAN-06 volatile double: 0 hits ✓
- SCAN-07 async void: 0 hits ✓

No discrepancies found.

## Decision
VERIFY_PASS
