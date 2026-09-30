# Ticket Review: BWAVE-DW LaneA

**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Source Tickets**: docs/brain/BWAVE-DW/LaneA/04-tickets.md
**Source Plan**: docs/brain/BWAVE-DW/LaneA/02-architecture-plan.md (REVIEW_PASS v2)
**Date**: 2026-09-03
**Physical Code Sources Read**:
  - `src/PropTraderTools/TradeCopierPanel.cs` lines 560–621
  - `src/PropTraderTools/TradeCopierWindow.cs` lines 407–465, 619–715, 894–910

---

## T1 — DW-C38-03: Remove cross-panel BE disarm loop in Detach

### Traceability
PASS — Ticket maps to spec requirement A-1 (DW-C38-03). Plan section "T1 — Fix A-1" corresponds
exactly. No phantom work.

### JS Pre-Check
PASS — No `lock()`, no `async void`, no `return null` in new code. T1 is a pure deletion with a
replacement comment block. All JS rules declared in the ticket are correctly scoped and accurate.

### CYC Pre-Check
**FAIL**

Claimed: Before=6, After=4, Delta=-2.

**Physical check refutes this claim.** The `Account.All` inline loop (branches 5 and 6 in the
ticket's branch list) no longer exists as inline code in `Detach()`. A prior BWAVE-CYC extraction
already moved the loop into `DisarmAllAccounts()`. At the time of ticket authoring, the Detach
method at line 610 reads:

```csharp
DisarmAllAccounts();
```

The two branches (`if (Account.All != null)` and `foreach (var acc in Account.All)`) are inside
`DisarmAllAccounts()`, which is a separate method. Deleting the `DisarmAllAccounts()` call removes
only ONE branch (the method call itself contributes 0 decision points — it is a straight-line call).
The CYC delta of -2 is therefore incorrect. The actual delta from deleting the call and replacing it
with a comment is 0 (no branch removed from `Detach()`). The CYC Before value claimed (6) cannot
be verified against the current source.

### NT8 Check
PASS — No new NT8 API introduced. The deleted code used `Account.All` (AddOnBase-available
enumerable) and `CopyEngine.Instance.DisarmPendingBe` (PTT-internal). Both are being removed.

### Physical Code Check
**FAIL (two violations)**

**Violation 1 — OLD code block is stale (does not match source file):**

The ticket states the OLD code at lines 610–614 is:
```csharp
// B40: disarm all accounts on detach (BE ALL global cleanup). NT8-043: no null-conditional compound.
// DW-B72-02: _globalBeState removed -- truth is IsPendingSlotsEmpty(). No local reset needed.
if (Account.All != null)
    foreach (var acc in Account.All)
        CopyEngine.Instance.DisarmPendingBe(acc);
```

Actual source at lines 608–611:
```csharp
// B40: disarm all accounts on detach (BE ALL global cleanup). NT8-043: no null-conditional compound.
// DW-B72-02: _globalBeState removed -- truth is IsPendingSlotsEmpty(). No local reset needed.
DisarmAllAccounts();
// No visual update here -- panel is being destroyed.
```

The inline loop was already extracted into `DisarmAllAccounts()` (BWAVE-CYC extraction, confirmed
at lines 633–642). The engineer who applies this ticket with the stated OLD block will not find it —
the patch will fail to locate the target code. The entire diff description in T1 is based on stale
source.

**Violation 2 — Scoped disarm line reference is wrong:**

The ticket states: "Do NOT modify line 593 (`_engine.DisarmPendingBe(_leaderAccount)`)."
Actual source: `_engine.DisarmPendingBe(_leaderAccount)` is at **line 591**, not line 593.
Line 593 is `_engine.CopyEnabledChanged -= OnCopyEnabledChanged;`.

This mismatch means the engineer's preservation guard references the wrong line number.

**Violation 3 — Method signature block is a placeholder:**

The ticket's "Method Signature" section contains only:
```
// The Detach/teardown method that contains the change.
// Exact signature to be confirmed by engineer from file.
// The method name is the NT8 lifecycle detach handler in TradeCopierPanel.
```

No actual signature is provided. The method signature is `public void Detach()` (confirmed at
line 577 of source). The engineer contract requires exact method signatures — a placeholder fails
this requirement.

### Test Coverage
PASS — Two named xUnit [Fact] tests are provided and map to the acceptance criteria:
- `DetachPanel_DoesNotDisarmSiblingPanelBeState()` — covers A/C item 3 (sibling isolation)
- `DetachPanel_DisarmsOwnLeaderAccount()` — covers A/C item 2 (own leader disarm)

Note: These tests are structurally valid as assertions. However, because the fix target is `DisarmAllAccounts()` (not an inline loop), the test harness setup must create conditions under which that extracted method is exercised. The test spec does not mention this extraction — engineers may need to stub `DisarmAllAccounts` or the full `Detach()` path. This is a WARNING, not a FAIL.

### Scan Checklist
PASS — All SCAN-01 through SCAN-07 are present in the ticket with commands, required results, and
status. Defense-in-depth contract is intact.

### File Routing
PASS — `src/PropTraderTools/TradeCopierPanel.cs` is correct Wave workspace path.

### VERDICT: **TICKET_REVIEW_FAIL**

**Blocking violations:**
1. OLD code block does not match the actual source file (stale — inline loop already extracted).
2. CYC Before/After/Delta claim is incorrect (loop is no longer inline in `Detach()`).
3. Scoped disarm preservation guard references wrong line number (591, not 593).
4. Method signature section is a placeholder, not a concrete signature.

---

## T2 — DW-C39-05: Re-apply feature flags after OnAddRule adds new row

### Traceability
PASS — Ticket maps to spec requirement A-2 (DW-C39-05). Plan section "T2 — Fix A-2" corresponds
exactly. No phantom work.

### JS Pre-Check
PASS — No `lock()`, no `async void` (non-event-handler), no `return null`. `OnAddRule` is a
`RoutedEventHandler` — the `async void` event-handler exemption (JS-033) is correctly noted and
does not apply here (the method is synchronous). `CopyEngine.Instance.Flags` is a value property.
All JS constraints are correctly assessed.

### CYC Pre-Check
PASS — Before=1, After=1, Delta=0. `ApplyFeatureFlags(...)` is a straight-line call; no branch
is added. CYC stays at 1, well within the CYC ≤ 8 limit.

### NT8 Check
PASS — No NT8 API introduced. `CopyEngine.Instance.Flags` is PTT-internal. `ApplyFeatureFlags`
is a UI-thread-only helper. No Dispatcher wrapping needed (confirmed: `OnAddRule` is a WPF
`RoutedEventHandler`, guaranteed UI thread). No banned NT8 API.

### Physical Code Check
**FAIL (two violations)**

**Violation 1 — Line range is wrong:**

The ticket states: "lines 1039–1042." The actual `OnAddRule` method is at **lines 898–901**:

```csharp
// Actual source (TradeCopierWindow.cs line 898-901):
private void OnAddRule(object sender, RoutedEventArgs e)
{
    _rulesPanel.Children.Add(BuildDynamicRuleRow());
}
```

The stated OLD code block content is correct (it matches the actual method body), but the line
numbers are wrong by 141 lines. An engineer applying the patch by line number will edit the wrong
location.

**Violation 2 — Test assertions target wrong button list:**

The ticket's [Fact] tests assert:
```
Assert: all buttons in _armBeBtns have IsEnabled == false.
Assert: all buttons in _tightenBtns have IsEnabled == false.
```

Physical code check reveals that `ApplyFeatureFlags` (the method being called by the fix) iterates
`_beBtns` for the BreakEven flag gate — NOT `_armBeBtns`:

```csharp
// ApplyFeatureFlags (TradeCopierWindow.cs lines 425-441):
private void ApplyFeatureFlags(FeatureFlags f)
{
    ApplyButtonGroupFlag(_trimBtns, f.TrimFlatten, "Trim requires Pro tier");
    ApplyButtonGroupFlag(_flattenBtns, f.TrimFlatten, "Trim/Flatten requires Pro tier");
    ApplyButtonGroupFlag(_cancelBtns, f.TrimFlatten, "Cancel requires Pro tier");
    ApplyButtonGroupFlag(_beBtns, f.BreakEven, "Break Even requires Pro tier");
    // ... _modeCb, _addRuleBtn only
}
```

`_armBeBtns` and `_tightenBtns` are **never passed to `ApplyButtonGroupFlag`**. These lists are
populated by `BuildArmBeCluster` and `BuildTightenCluster` respectively (confirmed lines 677, 710).
The fix (`ApplyFeatureFlags(CopyEngine.Instance.Flags)`) will NOT disable `_armBeBtns` or
`_tightenBtns` buttons on a Starter-tier user — it gates `_beBtns` only.

This means:
- The acceptance criteria ("Starter-tier user cannot access Arm BE or Tighten buttons on any
  dynamically-added rule row") will NOT be satisfied by calling `ApplyFeatureFlags` alone.
- The [Fact] tests as written will FAIL even if the implementation is correct (the lists being
  asserted are never touched by the fix).
- Either (a) the fix is incomplete and `ApplyFeatureFlags` must be extended to gate `_armBeBtns`
  and `_tightenBtns`, or (b) the spec requirement is about `_beBtns` only and the test names are
  wrong. Either way this is a contract mismatch that must be resolved before engineering.

### Test Coverage
**FAIL** — See Physical Code Check Violation 2 above. The three [Fact] tests assert against
`_armBeBtns` and `_tightenBtns`, but `ApplyFeatureFlags` does not gate those lists. Tests that
assert unreachable state transitions are not valid coverage. The test contract must be corrected to
match either the actual fix scope (`_beBtns`) or the fix must be extended to cover `_armBeBtns`
and `_tightenBtns`.

### Scan Checklist
PASS — All SCAN-01 through SCAN-07 are present in the ticket with commands, required results, and
status. Defense-in-depth contract is intact.

### File Routing
PASS — `src/PropTraderTools/TradeCopierWindow.cs` is correct Wave workspace path.

### VERDICT: **TICKET_REVIEW_FAIL**

**Blocking violations:**
1. Line range is wrong: ticket states 1039–1042, actual source is 898–901.
2. Test assertions target `_armBeBtns` / `_tightenBtns` but `ApplyFeatureFlags` gates `_beBtns`
   only — fix does not satisfy acceptance criteria A-2.1 as written.

---

## Overall: TICKET_REVIEW_FAIL

Both tickets fail. No ticket may proceed to engineering.

### Violation Summary (architect must resolve all before resubmit)

| # | Ticket | Item | Violation |
|---|--------|------|-----------|
| V-1 | T1 | OLD code block | Stale — `Account.All` inline loop was already extracted into `DisarmAllAccounts()`. Line 610 now reads `DisarmAllAccounts();`. The OLD block does not exist in the source file. |
| V-2 | T1 | CYC Before/After/Delta | Incorrect — loop is no longer inline in `Detach()`. CYC delta from removing the `DisarmAllAccounts()` call is 0, not -2. Claimed branch count 6 cannot be verified. |
| V-3 | T1 | Scoped disarm line reference | Wrong — `_engine.DisarmPendingBe(_leaderAccount)` is at line 591, not line 593. |
| V-4 | T1 | Method signature | Placeholder only. Actual signature is `public void Detach()` (line 577). Must be stated explicitly. |
| V-5 | T2 | Line range | Wrong — `OnAddRule` is at lines 898–901, not 1039–1042. |
| V-6 | T2 | Acceptance criteria / test assertions | `ApplyFeatureFlags` gates `_beBtns`, not `_armBeBtns` or `_tightenBtns`. The three [Fact] tests assert state that `ApplyFeatureFlags` cannot produce. Fix is either incomplete (must also gate `_armBeBtns`/`_tightenBtns`) or spec requirement A-2 targets only `_beBtns` and test names must be corrected. |

### Required Architect Actions Before Resubmit

1. **T1-V1/V2**: Re-read the current source. The fix target is `DisarmAllAccounts()` (the extracted
   method). Update OLD block to `DisarmAllAccounts();` (single call). Update NEW block to the comment
   replacement. Recalculate CYC — if `DisarmAllAccounts()` is deleted entirely, the branch removal
   is in that method (CYC of `DisarmAllAccounts` = 2), not in `Detach()`. Consider whether the
   correct fix is: (a) replace `DisarmAllAccounts()` body with scoped logic (if needed), or
   (b) delete the `DisarmAllAccounts()` call from `Detach()` entirely (which was the intent).
2. **T1-V3**: Correct line reference from 593 to 591.
3. **T1-V4**: Replace placeholder method signature with `public void Detach()` (line 577).
4. **T2-V5**: Correct line range from 1039–1042 to 898–901.
5. **T2-V6**: Investigate whether `_armBeBtns` and `_tightenBtns` should be added to
   `ApplyFeatureFlags`. If yes: expand fix to add two `ApplyButtonGroupFlag` calls; update CYC.
   If no: scope acceptance criteria and tests to `_beBtns` only and rename tests accordingly.

---

*Reviewed by ptt-ticket-reviewer. This document is READ-ONLY output. Architect owns all corrections.*

---

## Cycle 2 Review — BWAVE-DW LaneA

**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Source Tickets**: docs/brain/BWAVE-DW/LaneA/04-tickets.md (architect v3 rewrite)
**Source Plan**: docs/brain/BWAVE-DW/LaneA/02-architecture-plan.md (REVIEW_PASS v3)
**Date**: 2026-09-03
**Physical Code Sources Read (Cycle 2)**:
  - `src/PropTraderTools/TradeCopierPanel.cs` lines 577–650
  - `src/PropTraderTools/TradeCopierWindow.cs` lines 420–445, 893–910

---

### Cycle-1 Violation Resolution Check

| V# | Ticket | Violation | Resolved? | Evidence |
|----|--------|-----------|-----------|----------|
| V-1 | T1 | OLD code block stale (inline loop) | YES | Ticket OLD block now shows `DisarmAllAccounts();` — source line 610 reads `DisarmAllAccounts();` exactly |
| V-2 | T1 | CYC Before/After/Delta incorrect | YES | Ticket now states Detach CYC=5→5 (delta 0; removing a method call adds no branch), DisarmAllAccounts CYC=2→deleted. Branch enumeration is concrete and verified (5 branches in Detach confirmed in source). |
| V-3 | T1 | Scoped disarm guard references wrong line (593 vs 591) | YES | Ticket now states "line 591". Source confirms `_engine.DisarmPendingBe(_leaderAccount)` is at line 591. |
| V-4 | T1 | Method signature section was placeholder | YES | Ticket now explicitly states `public void Detach() // line 577` and `private static void DisarmAllAccounts() // lines 636–642`. Both confirmed in source. |
| V-5 | T2 | OnAddRule line range wrong (1039–1042 stated, actual 898–901) | YES | Ticket now states 898–901 for both Part B OLD/NEW blocks. Source confirms OnAddRule at lines 898–901. |
| V-6 | T2 | Fix incomplete — _armBeBtns/_tightenBtns not gated by ApplyFeatureFlags | YES | Architect chose option (a): Part A of T2 adds two `ApplyButtonGroupFlag` calls for `_armBeBtns` and `_tightenBtns` inside `ApplyFeatureFlags`. Source confirms these lists are currently absent from ApplyFeatureFlags (line 430 = `_beBtns` only). NEW code block correctly adds them. [Fact] tests now assert the gated lists that WILL be affected. |

---

### T1 — DW-C38-03 (Cycle 2)

**Traceability**: PASS — Ticket maps to spec requirement A-1 (DW-C38-03). No phantom work.

**JS Pre-Check**: PASS — No `lock()`, no `async void`, no `return null`. T1 is a pure deletion
with a 2-line comment replacement. JS-021, JS-033, JS-002, JS-001 all confirmed PASS.

**CYC Pre-Check**: PASS
- `Detach` CYC = 5 before, 5 after (delta 0). Five branches enumerated and verified:
  (1) `if (_currentChart != null)`, (2) `if (_leaderAccount != null)`,
  (3) `if (_accountCombo != null && ...)`, (4) `&&` short-circuit, (5) `foreach (IPttModule m in _modules)`.
  Removing `DisarmAllAccounts()` call at line 610 is a straight-line call removal — zero branch delta.
- `DisarmAllAccounts` CYC = 2, deleted entirely. Claim is correct.
- No method exceeds CYC 8.

**NT8 Check**: PASS — No new NT8 API calls introduced. Removed calls (`Account.All`,
`CopyEngine.Instance.DisarmPendingBe`) are PTT/NT8 but are being deleted, not added.

**Physical Code Check**: PASS
- Line 577: `public void Detach()` confirmed — matches ticket signature.
- Line 591: `_engine.DisarmPendingBe(_leaderAccount)` confirmed — preservation guard is correct.
- Line 610: `DisarmAllAccounts();` confirmed — OLD block 1 in ticket matches exactly.
- Lines 636–642: `private static void DisarmAllAccounts() { ... }` confirmed — OLD block 2 in ticket
  matches exactly (6-line body: declaration, `{`, `if (Account.All == null) return;`,
  `foreach (var acc in Account.All)`, `CopyEngine.Instance.DisarmPendingBe(acc);`, `}`).

**Test Coverage**: PASS — Two named xUnit [Fact] tests present:
- `DetachPanel_DoesNotDisarmSiblingPanelBeState()` — covers A/C item 1 (sibling isolation)
- `DetachPanel_DisarmsOwnLeaderAccount()` — covers A/C item 2 (own leader disarm)

**Scan Checklist**: PASS — SCAN-01 through SCAN-07 all present with commands, required results,
and status. Defense-in-depth contract intact.

**File Routing**: PASS — `src/PropTraderTools/TradeCopierPanel.cs` is correct Wave workspace path.

**VERDICT: TICKET_REVIEW_PASS**

---

### T2 — DW-C39-05 (Cycle 2)

**Traceability**: PASS — Ticket maps to spec requirement A-2 (DW-C39-05). No phantom work.

**JS Pre-Check**: PASS — No `lock()`, no `async void` (non-event-handler), no `return null`.
`OnAddRule` is a `RoutedEventHandler` (JS-033 exemption correctly noted; method is synchronous).
`CopyEngine.Instance.Flags` is a value property — no null-return risk. JS-021, JS-033, JS-002,
JS-001 all confirmed PASS.

**CYC Pre-Check**: PASS
- `ApplyFeatureFlags` CYC = 5 before, 5 after (delta 0). The two added `ApplyButtonGroupFlag(...)` calls
  are straight-line statements — zero branch delta. Existing 4 branch points
  (`if (_modeCb != null)`, `if (_addRuleBtn != null)`, `? null :` ternary x2) unchanged.
- `OnAddRule` CYC = 1 before, 1 after (delta 0). `ApplyFeatureFlags(...)` is a straight-line call.
- No method exceeds CYC 8.

**NT8 Check**: PASS — `CopyEngine.Instance.Flags` is PTT-internal. `ApplyFeatureFlags` is
UI-thread-only (already established). No NT8 API introduced. No Dispatcher wrapping required
(`OnAddRule` is a WPF RoutedEventHandler — guaranteed UI thread).

**Physical Code Check**: PASS
- `ApplyFeatureFlags` at line 425 confirmed: OLD block (lines 425–441) matches source exactly.
  `_armBeBtns` and `_tightenBtns` are confirmed absent from the current method — the two new
  lines in the NEW block correctly extend the coverage.
- `OnAddRule` at lines 898–901 confirmed: OLD block in ticket matches source exactly
  (4-line method: declaration, `{`, `_rulesPanel.Children.Add(BuildDynamicRuleRow());`, `}`).
- Part B comment placement (before `private void OnAddRule`) is syntactically valid.
- NEW blocks for both parts are unambiguous; no overlap or ordering conflict.

**Test Coverage**: PASS — Three named xUnit [Fact] tests present:
- `OnAddRule_StarterTier_NewRowArmBeButtonIsDisabled()` — gates `_armBeBtns` on Starter tier
- `OnAddRule_ProTier_NewRowArmBeButtonIsEnabled()` — gates `_armBeBtns` on Pro tier
- `OnAddRule_StarterTier_NewRowTightenButtonIsDisabled()` — gates `_tightenBtns` on Starter tier

All three assertions now target lists that WILL be gated by the Part A expansion of `ApplyFeatureFlags`.
Test contract is coherent with the fix.

**Scan Checklist**: PASS — SCAN-01 through SCAN-07 all present with commands, required results,
and status. Defense-in-depth contract intact.

**File Routing**: PASS — `src/PropTraderTools/TradeCopierWindow.cs` is correct Wave workspace path.

**VERDICT: TICKET_REVIEW_PASS**

---

### Full Checklist Summary (Cycle 2)

| Check | T1 | T2 |
|-------|----|----|
| Spec req ID present | A-1 (DW-C38-03) PASS | A-2 (DW-C39-05) PASS |
| File + line ranges correct | 610 + 636–642 PASS | 425–441 + 898–901 PASS |
| OLD code matches source | PASS (verified) | PASS (verified) |
| NEW code unambiguous | PASS | PASS |
| CYC concrete integers | Detach 5→5; DisarmAllAccounts 2→deleted PASS | ApplyFeatureFlags 5→5; OnAddRule 1→1 PASS |
| JS-021 confirmed | PASS | PASS |
| JS-033 confirmed | PASS | PASS |
| JS-002 confirmed | PASS | PASS |
| xUnit [Fact] names present | 2 tests PASS | 3 tests PASS |
| SCAN-01..07 all present | PASS | PASS |
| No scope creep | PASS | PASS |
| Post-gate (sync + F5) present | PASS | PASS |
| File routing to Wave workspace | PASS | PASS |

---

## Overall Cycle 2: **TICKET_REVIEW_PASS**

All 6 Cycle-1 violations resolved. Both tickets pass all checks. Engineering may proceed.

*Reviewed by ptt-ticket-reviewer (Cycle 2). This document is READ-ONLY output. Architect owns all corrections.*
