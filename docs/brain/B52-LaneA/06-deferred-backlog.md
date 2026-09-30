# B52-LaneA Deferred Backlog
## PTT-COPIER-B52 / Lane A — test-restore-extraction

**Block**: PTT-COPIER-B52
**Lane**: A
**Label**: test-restore-extraction
**Created by**: ptt-verifier (Phase 4b)
**Date**: 2026-08-08

---

## Closed Items — Implemented in B52

The following deferred items were opened in previous blocks and are now CLOSED by B52-LaneA.

| ID | Priority | Status | Description |
|---|---|---|---|
| DW-B50C-01 | P1 | CLOSED | **Restore `FindFollowerBracketOrder` test behavioral assertion**: `FindFollowerBracketOrder_ReturnsNullWhenNoMatch` replaces `FindFollowerBracketOrder_NullableReturnType`. Two assertions present: `Assert.Equal(typeof(NinjaTrader.Cbi.Order), method.ReturnType)` (type-level) + `Assert.Null(result)` (behavioral null contract). `TargetInvocationException` / `NullReferenceException` guard handles NT8 runtime absence. CYC(Lizard)=2. Layer 3 verified at line 429 of CopyEngineTests.cs. |
| DW-B51-03 | P2 | CLOSED | **Extract `PopulateAtmComboItems` and `ApplyAtmAutoSelect` from `OnFollowerAtmTemplateComboLoaded`**: Parent CYC reduced from 12 (McCabe) / 11 (Lizard) to 5/4. `PopulateAtmComboItems` CYC=5/4 (absorbs branches 5-8: dir-exists, foreach, leader-match, catch). `ApplyAtmAutoSelect` CYC=4/3 (absorbs branches 9-11: defaultIdx-guard, selName-guard, item-guard). All 11 branches preserved. Both helpers `private void`. `cb.SelectedIndex = defaultIdx` retained in parent between calls. Layer 3 verified in TradeCopierPanel.cs lines 1969-2060. |

---

## Carried-Forward Items (OPEN — No Priority Change)

These items were opened in B50 or earlier blocks. They are not addressed by B52 and remain open.

| ID | Priority | Status | Description |
|---|---|---|---|
| DW-B50-01 | P1 | OPEN | **Live F5 verification of Clone ATM cache**: Verify `GetLeaderAtmTemplateName(_currentChart)` correctly reads the leader's selected ATM template from the ChartTrader visual tree in a live NT8 session. Depends on DW-B43-02. Requires NT8 session with open chart and active market data. |
| DW-B50-02 | P2 | OPEN | **`_atmComboRefs` weak reference cleanup**: Replace `List<ComboBox>` with `List<WeakReference<ComboBox>>` and prune dead refs in `UpdateAtmComboVisibility`. No behavioral error; mild GC pressure only. |
| DW-B47-05 | P2 | OPEN | **`return null` in `FindRule`, `FindFollowerBracketOrder`, `TryResolveLeaderAccount`**: Convert to `Option<T>` / nullable reference pattern for full JS-002 compliance. Pre-existing from B47; not introduced by B52. |
| DW-B43-02 | P1 | OPEN | **Visual-tree index accuracy for `GetLeaderAtmTemplateName`**: ChartTrader ComboBox index in visual tree may shift on NT8 version updates. Blocking dependency for DW-B50-01. |

---

## Notes for Future Engineers

### DW-B50-01 — Live F5 Protocol

See `docs/brain/B50-LaneA/06-deferred-backlog.md` § Notes for full F5 protocol steps.
Key signal: absence of `"PTT-Clone: no ATM cache -- using Inherit fallback"` in StatusUpdate log.

### DW-B50-02 — Weak Reference Implementation

See `docs/brain/B50-LaneA/06-deferred-backlog.md` § DW-B50-02 for implementation pattern.
CYC impact: `UpdateAtmComboVisibility` rises from 2 to 4 after the change (still ≤ 8).

### DW-B47-05 — JS-002 return null Remediation

Three methods in `CopyEngine.cs` have pre-existing `return null` patterns:
- `FindRule(string instrument)` — returns null if no matching rule
- `FindFollowerBracketOrder(Account, string, bool)` — returns null if no match (now tested by B52)
- `TryResolveLeaderAccount()` in `TradeCopierPanel.cs` — returns null if combo unresolved

Converting these to `Option<T>` requires updating all call sites. Treat as a dedicated refactor
block, not a bug fix. All callers already handle null-as-sentinel correctly.

### DW-B43-02 — ChartTrader ComboBox Index

The `GetLeaderAtmTemplateName` method uses `FindVisualChild<ComboBox>` to locate ATM ComboBox at
visual-tree index. If NinjaTrader adds a ComboBox before the ATM one in a future version update,
index 2 will return the wrong element. Mitigation: scan by role/label rather than positional index.

---

## PIPELINE_COMPLETE Gate Reference

This file satisfies the `06-deferred-backlog.md` requirement for the B52 Lane A PIPELINE_COMPLETE gate.

**Gate check**:
- DW-B50C-01 and DW-B51-03 are documented as CLOSED (implemented and Layer-3-verified in B52).
- DW-B50-01, DW-B50-02, DW-B47-05, DW-B43-02 are carried forward with no priority change.
- None of the OPEN items is a P0 blocking issue.
- Ticket-1 and ticket-2 verification reports confirm VERIFY_PASS.
- Final review confirms FINAL_PASS.
- PIPELINE_COMPLETE is unblocked.
