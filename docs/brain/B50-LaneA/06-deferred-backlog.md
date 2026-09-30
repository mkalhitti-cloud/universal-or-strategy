# B50-LaneA Deferred Backlog
## PTT-COPIER-B50 / Lane A — Clone Mode

**Block**: PTT-COPIER-B50
**Lane**: A
**Label**: clone-mode
**Created by**: ptt-verifier (Phase 4b)
**Date**: 2026-08-08

---

## Deferred Items from B50 Lane A

These items were not implemented in B50 Lane A (clone-mode) and are carried forward to future blocks.

| ID | Priority | Status | Description |
|----|----------|--------|-------------|
| DW-B50-01 | P1 | OPEN | **Live F5 verification of Clone ATM cache**: Verify that `GetLeaderAtmTemplateName(_currentChart)` correctly reads the leader's currently-selected ATM template from the ChartTrader visual tree in a live NinjaTrader session. Depends on DW-B43-02 (visual-tree index accuracy for ChartTrader ComboBox). Cannot be verified in automated build environment — requires NT8 session with an open chart and active market data feed. When B51+ is opened, Director should run F5 and click Clone radio button to verify the cache populates and dispatch sends Named ATM. |
| DW-B50-02 | P2 | OPEN | **`_atmComboRefs` weak reference cleanup**: The `_atmComboRefs` list in `TradeCopierPanel.cs` retains hard references to `ComboBox` controls. If the followers panel is rebuilt (e.g., rules reloaded, accounts disconnected and reconnected), detached `ComboBox` instances remain in the list. No behavioral error occurs (detached WPF elements handle `Visibility` setters silently), but there is mild GC pressure. Future fix: replace `List<ComboBox>` with `List<WeakReference<ComboBox>>` and prune dead references in `UpdateAtmComboVisibility`. Alternatively, add a `ClearAtmComboRefs()` call to the panel teardown path. |

---

## Notes for Future Engineers

### DW-B50-01 — Live F5 Protocol

To close DW-B50-01 in a future block:
1. Open NinjaTrader 8 with F5 compilation of current PTT build.
2. Open a chart for the leader instrument.
3. Open TradeCopierPanel. Confirm the Mode row shows Signal / Mirror / Clone radio buttons.
4. Select Clone radio button.
5. Verify `StatusUpdate` does NOT emit "PTT-Clone: no ATM cache -- using Inherit fallback".
6. Place a market entry on the leader account.
7. Verify the follower order is placed with the expected ATM template name (Named mode).
8. Confirm ATM brackets appear on the follower account after fill.

If step 5 shows the fallback message, `GetLeaderAtmTemplateName` returned `string.Empty`. Investigate DW-B43-02 (ChartTrader visual tree ComboBox index).

### DW-B50-02 — Implementation Guidance

When implementing the weak reference cleanup:
- Change `private readonly List<System.Windows.Controls.ComboBox> _atmComboRefs` to `private readonly List<WeakReference<System.Windows.Controls.ComboBox>> _atmComboRefs`
- In `OnFollowerAtmTemplateComboLoaded`: `_atmComboRefs.Add(new WeakReference<ComboBox>(cb))`
- In `UpdateAtmComboVisibility`: iterate and prune dead references in the same pass
- CYC impact: `UpdateAtmComboVisibility` CYC rises from 2 to 4 (TryGetTarget + dead-ref prune branch). Still ≤ 8.

---

## Pre-Existing Debt (Carried Forward, Not Opened by B50)

The following items were pre-existing when B50 started and remain open:

| ID | Priority | Description |
|----|----------|-------------|
| DW-B47-05 | P2 | `return null` in `FindRule`, `FindFollowerBracketOrder`, `TryResolveLeaderAccount` — pre-existing before B50. Not introduced by B50. Should be converted to `Option<T>` or `CopyRule?` pattern in a future refactor block. |
| DW-B43-02 | P1 | Visual-tree index accuracy for `GetLeaderAtmTemplateName` — ComboBox index in ChartTrader may shift on NT8 version updates. Blocking dependency for DW-B50-01. |

---

## PIPELINE_COMPLETE Gate Reference

This file satisfies the `06-deferred-backlog.md` requirement for the B50 Lane A PIPELINE_COMPLETE gate.

**Gate check**: `DW-B50-01` and `DW-B50-02` are documented with rationale, reproduction steps, and implementation guidance. Neither is a P0 blocking issue. Both are deferred by design per the B50 architecture plan.
