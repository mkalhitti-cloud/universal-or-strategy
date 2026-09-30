# BWAVE-DW LaneA — Architecture Plan

**Status**: REVIEW_PASS (v3 — violations V-1 through V-6 fixed)
**Phase**: Phase 1 (Architecture)
**Epic**: BWAVE-DW LaneA — Surgical Fixes A-1 and A-2
**Date**: 2026-09-03
**Architect**: ptt-architect

---

## LANE-SPLIT GATE RESULT: LANES-APPROVED

| Q  | Question                                                        | Answer | Rationale |
|----|-----------------------------------------------------------------|--------|-----------|
| Q1 | Same method or within 50 lines of each other?                   | NO     | Different files: TradeCopierPanel.cs vs TradeCopierWindow.cs |
| Q2 | Does Fix A-2 design depend on Fix A-1 final design?             | NO     | Orthogonal data paths — teardown vs dynamic UI construction |
| Q3 | Does each fix have standalone value if the other is blocked?    | YES    | A-1 fixes cross-panel BE contamination independently; A-2 fixes license gate independently |
| Q4 | Does each fix have an independent SIM verification path?        | YES    | A-1: two-panel detach SIM test; A-2: Starter-tier dynamic-row SIM test |

**Conclusion**: Two independent tickets T1 (A-1) and T2 (A-2). Neither blocks the other.

---

## Spec Requirement Mapping

| Spec ID | Fix | Description |
|---------|-----|-------------|
| A-1     | T1  | DW-C38-03 — Detach disarms all accounts' BE slots |
| A-2     | T2  | DW-C39-05 — License gate not re-applied after OnAddRule |

---

## Component List

| Ticket | File | Class | Method Modified |
|--------|------|-------|-----------------|
| T1 | `src/PropTraderTools/TradeCopierPanel.cs` | `TradeCopierPanel` | `Detach` (line 577) — remove call at line 610 + delete `DisarmAllAccounts()` method at lines 636–642 |
| T2 | `src/PropTraderTools/TradeCopierWindow.cs` | `TradeCopierWindow` | `OnAddRule` (line 898) + `ApplyFeatureFlags` (lines 425–441) |

---

## T1 — Fix A-1: DW-C38-03

### Problem

During panel teardown, `Detach()` calls a private static helper `DisarmAllAccounts()` (line 610)
that iterates `Account.All` and calls `CopyEngine.Instance.DisarmPendingBe(acc)` on every
account in NinjaTrader. This contaminates the pending-BE state of sibling panels that own those
accounts. The line-591 call `_engine.DisarmPendingBe(_leaderAccount)` is the correct, scoped
disarm for this panel's leader account. `DisarmAllAccounts()` is purely the bug.

`DisarmPendingBe` is confirmed idempotent (null-guard + `TryRemove` no-op if absent), so
removing the call and method cannot leave orphaned state for the panel being torn down.

### Exact Change

**File**: `src/PropTraderTools/TradeCopierPanel.cs`

**Change 1 — Remove call at line 610 (inside `Detach`):**

**OLD (line 610):**
```csharp
            DisarmAllAccounts();
```

**NEW (line 610 replacement):**
```csharp
            // DW-C38-03: DisarmAllAccounts() call removed -- was disarming sibling panels' BE state (bug).
            // Leader-account disarm already performed at line 591 (_engine.DisarmPendingBe(_leaderAccount)).
```

**Change 2 — Delete `DisarmAllAccounts()` method definition (lines 636–642):**

**OLD (lines 636–642):**
```csharp
        private static void DisarmAllAccounts()
        {
            if (Account.All == null)
                return;
            foreach (var acc in Account.All)
                CopyEngine.Instance.DisarmPendingBe(acc);
        }
```

**NEW**: Delete entirely (no replacement).

**Net diff**: Remove 1 method call site + 7-line method definition. Replace call site with a
2-line comment recording the fix decision. No executable code is added.

### CYC Delta — T1

| Method | Before | After | Delta |
|--------|--------|-------|-------|
| `Detach` (line 577) | 5 | 5 | 0 |
| `DisarmAllAccounts` (lines 636–642) | 2 | 0 (deleted) | -2 |

**`Detach` branch count (CYC=5 before and after)**:
1. `if (_currentChart != null)` — branch 1
2. `if (_leaderAccount != null)` — branch 2
3. `if (_accountCombo != null && _accountComboSelectionChanged != null)` — branch 3
4. `&&` short-circuit operand — branch 4
5. `foreach (IPttModule m in _modules)` — branch 5

Removing line 610 (`DisarmAllAccounts()` call) removes 0 branches — it is a method call, not a
control flow branch. `Detach` CYC is unchanged at 5.

**`DisarmAllAccounts` branch count (CYC=2 before deletion)**:
1. `if (Account.All == null)` — branch 1
2. `foreach (var acc in Account.All)` — branch 2

Method deleted entirely. CYC drops to 0 (non-existent).

### NT8 API Usage — T1

No NT8 API calls are added. The deleted method called:
- `Account.All` — `AddOnBase`-available enumerable (removed, not added)
- `CopyEngine.Instance.DisarmPendingBe(acc)` — PTT internal (removed, not added)

No new NT8 API surface is introduced.

### Threading Model — T1

Teardown runs on the Dispatcher thread (NT8 lifecycle). No thread-safety issue is introduced
or removed. The fix is a pure deletion.

---

## T2 — Fix A-2: DW-C39-05

### Problem

`BuildDynamicRuleRow()` appends new `Button` instances to `_armBeBtns` (line 50) and
`_tightenBtns` (line 53) — the lists iterated by `ApplyButtonGroupFlag`. However,
`ApplyFeatureFlags` does not currently gate `_armBeBtns` or `_tightenBtns` at all
(confirmed by inspection of lines 425–441 — these two lists are absent). Additionally,
`ApplyFeatureFlags` is only called at `OnLoaded` (line 153) and when `FeatureFlagsChanged`
fires (line 453). A user can click "Add Rule" after load without the flags event firing — so
the new row's Arm BE and Tighten buttons are never gated. A Starter-tier user gets ungated
access to those buttons on every dynamically-added row.

### Exact Change — Two-Part Fix

**File**: `src/PropTraderTools/TradeCopierWindow.cs`

#### Part A — Expand `ApplyFeatureFlags` to gate `_armBeBtns` and `_tightenBtns`

**OLD (`ApplyFeatureFlags`, lines 425–441):**
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

**NEW (`ApplyFeatureFlags` — add 2 lines after the `_beBtns` call):**
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

**Net diff Part A**: +2 executable lines. No new branches. `ApplyFeatureFlags` CYC = 5→5.

#### Part B — Add re-gate call at end of `OnAddRule`

**OLD (`OnAddRule`, lines 898–901):**
```csharp
        private void OnAddRule(object sender, RoutedEventArgs e)
        {
            _rulesPanel.Children.Add(BuildDynamicRuleRow());
        }
```

**NEW (`OnAddRule`, lines 898–902):**
```csharp
        // DW-C39-05: re-gate new row buttons immediately after adding the row.
        private void OnAddRule(object sender, RoutedEventArgs e)
        {
            _rulesPanel.Children.Add(BuildDynamicRuleRow());
            ApplyFeatureFlags(CopyEngine.Instance.Flags); // gate newly-added buttons
        }
```

**Net diff Part B**: +1 executable line + 1 comment above the method declaration. `OnAddRule` CYC = 1→1.

### CYC Delta — T2

| Method | Before | After | Delta |
|--------|--------|-------|-------|
| `ApplyFeatureFlags` (line 425) | 5 | 5 | 0 |
| `OnAddRule` (line 898) | 1 | 1 | 0 |

`ApplyButtonGroupFlag(...)` calls are straight-line — no new branches. CYC stays at 5 for
`ApplyFeatureFlags` and 1 for `OnAddRule`.

### NT8 API Usage — T2

`CopyEngine.Instance.Flags` — PTT internal property. No NT8 API is introduced.
`ApplyFeatureFlags` runs synchronously on the UI thread (already the case for all callers per
its comment: "Called on UI thread only"). No Dispatcher wrapping required.

### Threading Model — T2

`OnAddRule` is a WPF `RoutedEventHandler` — guaranteed UI thread. `ApplyFeatureFlags` is also
UI-thread-only (documented). No threading change.

---

## JS Rule Compliance Checklist

| Rule | Applies To | Status |
|------|-----------|--------|
| JS-021: No `lock()` | T1, T2 | PASS — no lock introduced or present in modified methods |
| JS-033: No `async void` (non-event-handler) | T1, T2 | PASS — `OnAddRule` is a `RoutedEventHandler` (exempt); no async void introduced |
| JS-002: No `return null` | T1, T2 | PASS — neither fix returns a value |
| JS-001: No exception throws in hot path | T1, T2 | PASS — neither fix throws |

---

## xUnit [Fact] Tests

### T1 Tests

```
[Fact] DetachPanel_DoesNotDisarmSiblingPanelBeState()
  // Arrange: create two TradeCopierPanel instances, arm BE on panel B's leader account.
  // Act: call teardown/detach on panel A.
  // Assert: CopyEngine.IsPendingSlotArmed(panelBLeaderAccount) == true (unchanged).

[Fact] DetachPanel_DisarmsOwnLeaderAccount()
  // Arrange: arm BE on panel A's leader account.
  // Act: call teardown/detach on panel A.
  // Assert: CopyEngine.IsPendingSlotArmed(panelALeaderAccount) == false.
```

### T2 Tests

```
[Fact] OnAddRule_StarterTier_NewRowArmBeButtonIsDisabled()
  // Arrange: set FeatureFlags.BreakEven = false (Starter tier). Call OnLoaded equivalent to prime state.
  // Act: invoke OnAddRule (simulate button click).
  // Assert: all buttons in _armBeBtns are disabled (IsEnabled == false).

[Fact] OnAddRule_ProTier_NewRowArmBeButtonIsEnabled()
  // Arrange: set FeatureFlags.BreakEven = true (Pro tier). Prime state.
  // Act: invoke OnAddRule.
  // Assert: all buttons in _armBeBtns are enabled (IsEnabled == true).

[Fact] OnAddRule_StarterTier_NewRowTightenButtonIsDisabled()
  // Arrange: set FeatureFlags.BreakEven = false (Starter tier). Prime state.
  // Act: invoke OnAddRule.
  // Assert: all buttons in _tightenBtns are disabled (IsEnabled == false).
```

---

## 7-Scan Checklist (per ticket)

### T1 — TradeCopierPanel.cs teardown fix

| Scan | Check | Result |
|------|-------|--------|
| SCAN-01 CYC | `Detach` CYC stays at 5 (method call removed, not a branch). `DisarmAllAccounts` deleted (CYC 2→0). No method exceeds limit. | PASS |
| SCAN-02 lock | No `lock()` in removed or surrounding code. | PASS |
| SCAN-03 async-void | No async void in teardown path. | PASS |
| SCAN-04 null-return | No return value in teardown path. | PASS |
| SCAN-05 ASCII | Comment text added is ASCII-only. No Unicode, emoji, curly quotes. | PASS |
| SCAN-06 NT8-API | No new NT8 API calls introduced. Removed calls were `Account.All` + `DisarmPendingBe` (both safe to remove per spec). | PASS |
| SCAN-07 test-coverage | Two [Fact] tests cover sibling-isolation and self-disarm postconditions. | PASS |

### T2 — TradeCopierWindow.cs OnAddRule fix

| Scan | Check | Result |
|------|-------|--------|
| SCAN-01 CYC | `OnAddRule` stays at CYC=1. `ApplyFeatureFlags` stays at CYC=5 (2 new straight-line calls, no branches). | PASS |
| SCAN-02 lock | No `lock()` in `OnAddRule` or `ApplyFeatureFlags`. | PASS |
| SCAN-03 async-void | `OnAddRule` is `private void` event handler — no async. | PASS |
| SCAN-04 null-return | `CopyEngine.Instance.Flags` is a value property; no null return possible. | PASS |
| SCAN-05 ASCII | Added code and comments are ASCII-only. | PASS |
| SCAN-06 NT8-API | No NT8 API calls introduced. `CopyEngine.Instance.Flags` is PTT-internal. | PASS |
| SCAN-07 test-coverage | Three [Fact] tests cover Starter/Pro gate on arm-BE and tighten buttons for new rows. | PASS |

---

## Data Flow

```
T1 — DETACH PATH:
  Detach()  [line 577]
    -> _engine.DisarmPendingBe(_leaderAccount)   [line 591 -- KEPT, scoped disarm]
    -> DisarmAllAccounts()                        [line 610 -- CALL DELETED]
    -> DisarmAllAccounts() method body            [lines 636-642 -- METHOD DELETED]
    -> Sibling panels' BE state: UNAFFECTED

T2 — ADD-RULE PATH (AFTER FIX):
  OnAddRule(sender, e)  [line 898]
    -> _rulesPanel.Children.Add(BuildDynamicRuleRow())   [appends buttons to _armBeBtns, _tightenBtns]
    -> ApplyFeatureFlags(CopyEngine.Instance.Flags)       [ADDED -- gates all buttons in lists]

  ApplyFeatureFlags(f)  [line 425 -- EXPANDED]:
    -> ApplyButtonGroupFlag(_armBeBtns, f.BreakEven, ...)  [ADDED line]
    -> ApplyButtonGroupFlag(_tightenBtns, f.BreakEven, ...)  [ADDED line]
```

---

## Acceptance Criteria

| Fix | Acceptance |
|-----|-----------|
| A-1 | Detaching panel X does not affect pending BE state of panel Y. Panel X's own leader-account BE state is correctly disarmed at line 591. |
| A-2 | Starter-tier users cannot access Arm BE or Tighten on any dynamically-added rule row. Pro/Elite tiers retain full access. |

---

## Post-Implementation Gate (Mandatory — Both Tickets)

Both steps below are **blocking conditions** before any ticket is considered closed.

1. **Run sync-and-verify**:
   ```powershell
   powershell -File scripts\ptt-sync-and-verify.ps1
   ```
   Required result: `18/18 OK, 0 MISMATCH`.

2. **Press F5 in NinjaTrader 8 to recompile.**
   Required result: No new compilation errors.
   Only pre-existing B75/B76/B77 test errors are acceptable.

---

## Key Decisions

1. **T1 is a pure deletion** — remove the `DisarmAllAccounts()` call (line 610) and its method
   definition (lines 636–642). The line-591 scoped disarm already handles the leader account
   correctly. Adding a replacement loop was considered and rejected: follower accounts never have
   BE armed in PTT's current design.
2. **T2 is a two-part fix** — Part A adds the missing `_armBeBtns` and `_tightenBtns` gates to
   `ApplyFeatureFlags` (which also corrects the static rows globally); Part B calls
   `ApplyFeatureFlags` at the end of `OnAddRule` to re-gate all buttons after a dynamic row is
   added. No new helper, no new parameter, no new branch.
3. **No Dispatcher.InvokeAsync wrapping needed for T2** — `OnAddRule` is a WPF routed event
   handler, guaranteed UI thread.
