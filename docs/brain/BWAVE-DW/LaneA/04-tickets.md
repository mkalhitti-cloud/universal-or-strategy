# BWAVE-DW LaneA — Implementation Tickets

**Status**: TICKETS_COMPLETE
**Source Plan**: docs/brain/BWAVE-DW/LaneA/02-architecture-plan.md (REVIEW_PASS v3)
**Tickets**: 2 (T1 and T2 — independent, no execution order dependency)
**Date**: 2026-09-03
**Author**: ptt-architect

---

## TICKET T1 — DW-C38-03

### Title
Remove cross-panel BE disarm: delete `DisarmAllAccounts()` call and method

### Spec Requirement
**A-1** — DW-C38-03: Detach disarms all accounts' BE slots (bug: contaminates sibling panels)

### File + Line Ranges
- `src/PropTraderTools/TradeCopierPanel.cs`, line 610 — remove the `DisarmAllAccounts()` call
- `src/PropTraderTools/TradeCopierPanel.cs`, lines 636–642 — delete the `DisarmAllAccounts()` method definition

### Method Signatures (reference — not modified structurally)
```csharp
// Enclosing method (call site removal):
public void Detach()  // line 577 — the DisarmAllAccounts() call at line 610 is removed from here

// Method being deleted entirely:
private static void DisarmAllAccounts()  // lines 636–642 — delete whole method
```

> **Engineer note**: Do NOT modify line 591 (`_engine.DisarmPendingBe(_leaderAccount)`). That call is correct and must be preserved exactly as-is.

### Exact OLD Code Block 1 — Call site (line 610, inside `Detach`)
```csharp
            DisarmAllAccounts();
```

### Exact NEW Code Block 1 — Replace call site (line 610)
```csharp
            // DW-C38-03: DisarmAllAccounts() call removed -- was disarming sibling panels' BE state (bug).
            // Leader-account disarm already performed at line 591 (_engine.DisarmPendingBe(_leaderAccount)).
```

### Exact OLD Code Block 2 — Method definition (lines 636–642)
```csharp
        private static void DisarmAllAccounts()
        {
            if (Account.All == null)
                return;
            foreach (var acc in Account.All)
                CopyEngine.Instance.DisarmPendingBe(acc);
        }
```

### Exact NEW Code Block 2 — Delete entirely (no replacement)
*(no code — the entire method at lines 636–642 is deleted)*

> **Net diff**: Replace 1-line call site with a 2-line comment; delete the 7-line method definition. No executable code is added.

### CYC Before / After
| Method | Before | After | Delta |
|--------|--------|-------|-------|
| `Detach` (line 577) | 5 | 5 | 0 |
| `DisarmAllAccounts` (lines 636–642) | 2 | 0 (deleted) | -2 |

**`Detach` branches (CYC=5, unchanged)**:
1. `if (_currentChart != null)` — branch 1
2. `if (_leaderAccount != null)` — branch 2
3. `if (_accountCombo != null && _accountComboSelectionChanged != null)` — branch 3
4. `&&` short-circuit operand — branch 4
5. `foreach (IPttModule m in _modules)` — branch 5

Removing the `DisarmAllAccounts()` call at line 610 removes 0 branches (it is a method call, not a control flow branch). `Detach` CYC stays at 5.

**`DisarmAllAccounts` branches (CYC=2, then deleted)**:
1. `if (Account.All == null)` — branch 1
2. `foreach (var acc in Account.All)` — branch 2

### Acceptance Criteria
1. Detaching panel X does **not** affect the pending BE state of panel Y (sibling isolation).
2. Panel X's own leader-account BE state is correctly disarmed at line 591 (unchanged).
3. `CopyEngine.IsPendingSlotArmed(panelYLeaderAccount)` returns `true` after panel X is detached, when panel Y had BE armed before the detach.

### JS Rule Constraints
| Rule | Status | Rationale |
|------|--------|-----------|
| JS-021: No `lock()` | PASS | No lock present in teardown path; none introduced |
| JS-033: No `async void` (non-event-handler) | PASS | Teardown is synchronous; no async introduced |
| JS-002: No `return null` | PASS | Teardown path returns void |
| JS-001: No exception throws in hot path | PASS | No throws introduced |

### xUnit [Fact] Test Names
```
[Fact] DetachPanel_DoesNotDisarmSiblingPanelBeState()
  // Arrange: create two TradeCopierPanel instances.
  //          Arm BE on panel B's leader account via CopyEngine.
  // Act:     call teardown/detach on panel A.
  // Assert:  CopyEngine.IsPendingSlotArmed(panelBLeaderAccount) == true (unchanged).

[Fact] DetachPanel_DisarmsOwnLeaderAccount()
  // Arrange: arm BE on panel A's leader account via CopyEngine.
  // Act:     call teardown/detach on panel A.
  // Assert:  CopyEngine.IsPendingSlotArmed(panelALeaderAccount) == false.
```

### 7-Scan Checklist

| # | Scan | Command | Required Result | Status |
|---|------|---------|-----------------|--------|
| SCAN-01 | CYC | `python scripts/complexity_audit.py` | `Detach` CYC = 5 (<= 8); `DisarmAllAccounts` method no longer exists | PENDING — run post-implementation |
| SCAN-02 | lock() | `grep -r "lock(" src/ --include="*.cs"` | Zero matches in modified file | PENDING — run post-implementation |
| SCAN-03 | async void | `grep -rn "async void " src/ --include="*.cs"` | Zero matches in new code | PENDING — run post-implementation |
| SCAN-04 | return null | `grep -rn "return null;" src/ --include="*.cs"` | Zero matches in new code | PENDING — run post-implementation |
| SCAN-05 | ASCII | `powershell scripts/check_ascii.ps1` | Zero non-ASCII characters | PENDING — run post-implementation |
| SCAN-06 | NT8 API | Inspect diff: no new NT8 API calls added | No banned NT8 API (pure deletion) | PASS — T1 is a deletion; no new NT8 API surface introduced |
| SCAN-07 | Test coverage | xUnit [Fact] names match acceptance criteria | Both [Fact] tests listed above are present and pass | PENDING — run post-implementation |

### Post-Implementation Gate (Blocking)
```powershell
# Step 1 — Sync and verify (must show 18/18 OK, 0 MISMATCH)
powershell -File scripts\ptt-sync-and-verify.ps1

# Step 2 — Recompile in NinjaTrader 8
# Press F5 in NT8. Required result: no new compilation errors.
# Only pre-existing B75/B76/B77 test errors are acceptable.
```

---

## TICKET T2 — DW-C39-05

### Title
Re-apply feature flags: gate `_armBeBtns`/`_tightenBtns` globally + re-gate after `OnAddRule`

### Spec Requirement
**A-2** — DW-C39-05: License gate not re-applied after OnAddRule (Starter-tier bypass on dynamic rows)

### File + Line Ranges
- `src/PropTraderTools/TradeCopierWindow.cs`, lines 425–441 — expand `ApplyFeatureFlags` (Part A)
- `src/PropTraderTools/TradeCopierWindow.cs`, lines 898–901 — expand `OnAddRule` (Part B)

### Method Signatures (both modified methods)
```csharp
private void ApplyFeatureFlags(FeatureFlags f)  // line 425 — Part A: add 2 lines
private void OnAddRule(object sender, RoutedEventArgs e)  // line 898 — Part B: add 1 line
```

---

### Part A — Expand `ApplyFeatureFlags` (lines 425–441)

#### Exact OLD Code Block — `ApplyFeatureFlags` (lines 425–441)
```csharp
        private void ApplyFeatureFlags(FeatureFlags f)
        {
            ApplyButtonGroupFlag(_trimBtns, f.TrimFlatten, "Trim requires Pro tier");
            ApplyButtonGroupFlag(_flattenBtns, f.TrimFlatten, "Trim/Flatten requires Pro tier");
            ApplyButtonGroupFlag(_cancelBtns, f.TrimFlatten, "Cancel requires Pro tier");
            ApplyButtonGroupFlag(_beBtns, f.BreakEven, "Break Even requires Pro tier");
            if (_modeCb != null)
            {
                _modeCb.IsEnabled = f.MirrorMode;
                _modeCb.ToolTip = f.MirrorMode ? null : "Mirror mode requires Elite tier";
            }
            if (_addRuleBtn != null)
            {
                _addRuleBtn.IsEnabled = f.MultiRule;
                _addRuleBtn.ToolTip = f.MultiRule ? null : "Multi-rule requires Pro tier";
            }
        }
```

#### Exact NEW Code Block — `ApplyFeatureFlags` (add 2 lines after `_beBtns` call)
```csharp
        private void ApplyFeatureFlags(FeatureFlags f)
        {
            ApplyButtonGroupFlag(_trimBtns, f.TrimFlatten, "Trim requires Pro tier");
            ApplyButtonGroupFlag(_flattenBtns, f.TrimFlatten, "Trim/Flatten requires Pro tier");
            ApplyButtonGroupFlag(_cancelBtns, f.TrimFlatten, "Cancel requires Pro tier");
            ApplyButtonGroupFlag(_beBtns, f.BreakEven, "Break Even requires Pro tier");
            ApplyButtonGroupFlag(_armBeBtns, f.BreakEven, "Arm Break-Even not available on this plan");
            ApplyButtonGroupFlag(_tightenBtns, f.BreakEven, "Tighten Stop not available on this plan");
            if (_modeCb != null)
            {
                _modeCb.IsEnabled = f.MirrorMode;
                _modeCb.ToolTip = f.MirrorMode ? null : "Mirror mode requires Elite tier";
            }
            if (_addRuleBtn != null)
            {
                _addRuleBtn.IsEnabled = f.MultiRule;
                _addRuleBtn.ToolTip = f.MultiRule ? null : "Multi-rule requires Pro tier";
            }
        }
```

> **Net diff Part A**: +2 executable lines inserted after the `_beBtns` `ApplyButtonGroupFlag` call. No branches added. `ApplyFeatureFlags` CYC = 5→5.

---

### Part B — Expand `OnAddRule` (lines 898–901)

#### Exact OLD Code Block — `OnAddRule` (lines 898–901)
```csharp
        private void OnAddRule(object sender, RoutedEventArgs e)
        {
            _rulesPanel.Children.Add(BuildDynamicRuleRow());
        }
```

#### Exact NEW Code Block — `OnAddRule` (lines 898–902)
```csharp
        // DW-C39-05: re-gate new row buttons immediately after adding the row.
        private void OnAddRule(object sender, RoutedEventArgs e)
        {
            _rulesPanel.Children.Add(BuildDynamicRuleRow());
            ApplyFeatureFlags(CopyEngine.Instance.Flags); // gate newly-added buttons
        }
```

> **Net diff Part B**: +1 executable line (`ApplyFeatureFlags(CopyEngine.Instance.Flags);`) + 1 comment line inserted before the `private void` declaration. Engineer must preserve the existing blank-line spacing convention in the file when inserting the comment. `OnAddRule` CYC = 1→1.

---

### CYC Before / After
| Method | Before | After | Delta |
|--------|--------|-------|-------|
| `ApplyFeatureFlags` (line 425) | 5 | 5 | 0 |
| `OnAddRule` (line 898) | 1 | 1 | 0 |

`ApplyButtonGroupFlag(...)` calls are straight-line — no branches added. Both CYC values are unchanged.

### Acceptance Criteria
1. A Starter-tier user (FeatureFlags.BreakEven = false) cannot access **Arm BE** or **Tighten** buttons on any dynamically-added rule row.
2. A Pro/Elite-tier user (FeatureFlags.BreakEven = true) retains full access to those buttons on dynamically-added rows.
3. `ApplyFeatureFlags` is called synchronously on the UI thread immediately after the new row is appended to `_rulesPanel.Children`.
4. Static rows (built at window init by `BuildStaticRuleRow`) also benefit from the Part A fix: `_armBeBtns` and `_tightenBtns` are now globally gated on every `ApplyFeatureFlags` call.

### JS Rule Constraints
| Rule | Status | Rationale |
|------|--------|-----------|
| JS-021: No `lock()` | PASS | No lock in `OnAddRule` or `ApplyFeatureFlags` |
| JS-033: No `async void` (non-event-handler) | PASS | `OnAddRule` is a `RoutedEventHandler` — the async-void exemption applies; no async introduced here |
| JS-002: No `return null` | PASS | `CopyEngine.Instance.Flags` is a value property; `OnAddRule` returns void |
| JS-001: No exception throws in hot path | PASS | No throws introduced |

### xUnit [Fact] Test Names
```
[Fact] OnAddRule_StarterTier_NewRowArmBeButtonIsDisabled()
  // Arrange: set FeatureFlags.BreakEven = false (Starter tier).
  //          Call OnLoaded equivalent to prime the window state.
  // Act:     invoke OnAddRule (simulate button click).
  // Assert:  all buttons in _armBeBtns have IsEnabled == false.

[Fact] OnAddRule_ProTier_NewRowArmBeButtonIsEnabled()
  // Arrange: set FeatureFlags.BreakEven = true (Pro tier).
  //          Prime window state.
  // Act:     invoke OnAddRule.
  // Assert:  all buttons in _armBeBtns have IsEnabled == true.

[Fact] OnAddRule_StarterTier_NewRowTightenButtonIsDisabled()
  // Arrange: set FeatureFlags.BreakEven = false (Starter tier).
  //          Prime window state.
  // Act:     invoke OnAddRule.
  // Assert:  all buttons in _tightenBtns have IsEnabled == false.
```

### 7-Scan Checklist

| # | Scan | Command | Required Result | Status |
|---|------|---------|-----------------|--------|
| SCAN-01 | CYC | `python scripts/complexity_audit.py` | `OnAddRule` CYC = 1 (<= 8); `ApplyFeatureFlags` CYC = 5 (<= 8) | PENDING — run post-implementation |
| SCAN-02 | lock() | `grep -r "lock(" src/ --include="*.cs"` | Zero matches in modified file | PENDING — run post-implementation |
| SCAN-03 | async void | `grep -rn "async void " src/ --include="*.cs"` | Zero matches in new code (`OnAddRule` is non-async) | PENDING — run post-implementation |
| SCAN-04 | return null | `grep -rn "return null;" src/ --include="*.cs"` | Zero matches in new code | PENDING — run post-implementation |
| SCAN-05 | ASCII | `powershell scripts/check_ascii.ps1` | Zero non-ASCII characters | PENDING — run post-implementation |
| SCAN-06 | NT8 API | Inspect diff: `CopyEngine.Instance.Flags` is PTT-internal | No banned NT8 API introduced | PASS — no NT8 API surface used in the added lines |
| SCAN-07 | Test coverage | xUnit [Fact] names match acceptance criteria | All three [Fact] tests listed above are present and pass | PENDING — run post-implementation |

### Post-Implementation Gate (Blocking)
```powershell
# Step 1 — Sync and verify (must show 18/18 OK, 0 MISMATCH)
powershell -File scripts\ptt-sync-and-verify.ps1

# Step 2 — Recompile in NinjaTrader 8
# Press F5 in NT8. Required result: no new compilation errors.
# Only pre-existing B75/B76/B77 test errors are acceptable.
```

---

## Summary

| Ticket | Spec Req | File | Lines Changed | CYC Before→After | Tests | Post-Gate |
|--------|----------|------|---------------|-------------------|-------|-----------|
| T1 | A-1 (DW-C38-03) | TradeCopierPanel.cs | 610 (call removed) + 636–642 (method deleted) | Detach 5→5 (0); DisarmAllAccounts 2→deleted | 2 [Fact] | sync 18/18 + F5 green |
| T2 | A-2 (DW-C39-05) | TradeCopierWindow.cs | 425–441 (Part A: +2 lines) + 898–901 (Part B: +1 line) | ApplyFeatureFlags 5→5; OnAddRule 1→1 | 3 [Fact] | sync 18/18 + F5 green |

**Execution order**: T1 and T2 are fully independent — either may be executed first or in parallel.
