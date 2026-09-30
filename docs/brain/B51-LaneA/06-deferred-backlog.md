# B51-LaneA Deferred Backlog
## PTT-COPIER-B51 / Lane A — ui-fixes

**Block**: PTT-COPIER-B51
**Lane**: A
**Label**: ui-fixes
**Created by**: ptt-plan-reviewer (Phase 5)
**Date**: 2026-08-08

---

## Closed Items — Implemented in B51

The following deferred items were opened by ptt-architect / ptt-engineer in previous blocks
and are now CLOSED by B51 implementation.

| ID | Priority | Status | Description |
|---|---|---|---|
| DW-B51-01 | P1 | CLOSED | **Multiplier TextBox hidden in `BuildCheckItemTemplate()`**: `multFactory.SetValue(FrameworkElement.VisibilityProperty, Visibility.Collapsed)` added at TradeCopierPanel.cs line 1891. TextBox and `OnFollowerMultiplierChanged` handler are preserved (not deleted). CYC delta = 0. |
| DW-B51-02 | P1 | CLOSED | **Clone ATM combo timing fix in `OnFollowerAtmTemplateComboLoaded`**: `if (CopyEngine.Instance.GetCopyMode() == CopyMode.Clone) cb.Visibility = Visibility.Collapsed` added inside the `!_atmComboRefs.Contains(cb)` block at TradeCopierPanel.cs line 1978. Newly-loaded ATM combos now receive the current Clone mode state at load time, closing the timing gap without touching the existing mode-toggle pathway. CYC delta = +1. |

---

## New Deferred Items — Opened by B51

### DW-B51-03 — `OnFollowerAtmTemplateComboLoaded` CYC=12 (pre-existing, extraction deferred)

| Field | Value |
|---|---|
| **ID** | DW-B51-03 |
| **Priority** | P1 |
| **Status** | OPEN |
| **Target Block** | B52+ (dedicated extraction ticket) |
| **File** | `src\PropTraderTools\TradeCopierPanel.cs` |
| **Method** | `OnFollowerAtmTemplateComboLoaded(object sender, RoutedEventArgs e)` (~line 1969) |
| **Pre-B51 CYC** | 11 (established in B46/B50 — 10 pre-existing branches) |
| **B51 delta** | +1 (Clone mode check — minimum required for DW-B51-02) |
| **Post-B51 CYC** | 12 |
| **CYC target** | ≤ 8 |
| **Excess** | 4 points above target |
| **Origin** | Pre-existing from B46/B50 — not introduced by B51 |
| **Per V12.23** | Extraction of pre-existing complexity is out of scope for a targeted bug-fix block |

**Branch inventory** (all 11 — basis for extraction plan):

| # | Branch | Line | Source |
|---|---|---|---|
| 1 | `if (cb == null) return;` — null guard | ~1972 | pre-existing |
| 2 | `if (cb.Items.Count > 0) return;` — idempotency guard | ~1973 | pre-existing |
| 3 | `if (!_atmComboRefs.Contains(cb))` — contains guard | ~1974 | pre-existing |
| 4 | `if (GetCopyMode() == CopyMode.Clone) cb.Visibility = Collapsed` | ~1978 | B51 NEW |
| 5 | `if (Directory.Exists(atmDir))` — dir guard | ~1993 | pre-existing |
| 6 | `foreach (var f in Directory.GetFiles(atmDir, "*.xml"))` — loop | ~1995 | pre-existing |
| 7 | `if (tName == leaderTemplate)` — leader match | ~1999 | pre-existing |
| 8 | `catch {}` — catch block | ~2002 | pre-existing |
| 9 | `if (defaultIdx > 0)` — leader-default | ~2003 | pre-existing |
| 10 | `if (!string.IsNullOrEmpty(selName))` — selName guard | ~2006 | pre-existing |
| 11 | `if (item != null)` — item guard | ~2008 | pre-existing |

**Suggested extraction** (achieves post-extraction CYC ≤ 5 on the parent method):

```
PopulateAtmComboItems(ComboBox cb, string leaderTemplate, out int defaultIdx)
  → extract branches 5–7 (directory scan + leader match)
  → CYC of extracted method ≈ 4 (dir guard + loop + leader-match + catch)
  → CYC of extracted method is bounded and testable in isolation

ApplyAtmAutoSelect(ComboBox cb, int defaultIdx)
  → extract branches 9–11 (defaultIdx write-back + AtmModeName assignment)
  → CYC of extracted method ≈ 3 (defaultIdx guard + selName guard + item guard)

Post-extraction OnFollowerAtmTemplateComboLoaded CYC:
  Remaining branches: 1 (null guard) + 2 (idempotency) + 3 (contains) + 4 (clone check)
  + 2 calls (PopulateAtmComboItems, ApplyAtmAutoSelect) — no branch contribution from calls
  CYC = 1 + 4 = 5 ✅ (within ≤8 target)
```

**Extraction prerequisite**: Both extracted methods receive plain value parameters (ComboBox reference,
string, int). They do not access `this` fields beyond what is passed in, or at most read
`CopyEngine.Instance.GetCopyMode()` (pure read, no side effects on engine state). No threading
concerns — all three methods remain on the WPF UI thread.

**Test implications**: No new xUnit tests required for the extraction (same rationale as B51 —
WPF RoutedEventHandler context requires live NT8 dispatcher). SCAN-01, SCAN-02, SCAN-05, SCAN-06,
SCAN-07 are the minimum required scans for the extraction ticket.

---

## Carried-Forward Items from B50 (OPEN)

These items were opened in B50 or earlier blocks. They are not addressed by B51 and remain open.

| ID | Priority | Status | Description |
|---|---|---|---|
| DW-B50-01 | P1 | OPEN | **Live F5 verification of Clone ATM cache**: Verify `GetLeaderAtmTemplateName(_currentChart)` correctly reads the leader's selected ATM template from the ChartTrader visual tree in a live NT8 session. Depends on DW-B43-02. Requires NT8 session with open chart and active market data. |
| DW-B50-02 | P2 | OPEN | **`_atmComboRefs` weak reference cleanup**: Replace `List<ComboBox>` with `List<WeakReference<ComboBox>>` and prune dead refs in `UpdateAtmComboVisibility`. No behavioral error; mild GC pressure only. |
| DW-B47-05 | P2 | OPEN | **`return null` in `FindRule`, `FindFollowerBracketOrder`, `TryResolveLeaderAccount`**: Convert to `Option<T>` / `CopyRule?` nullable pattern. Pre-existing from B47; not introduced by B50 or B51. |
| DW-B43-02 | P1 | OPEN | **Visual-tree index accuracy for `GetLeaderAtmTemplateName`**: ChartTrader ComboBox index in visual tree may shift on NT8 version updates. Blocking dependency for DW-B50-01. |

---

## Notes for Future Engineers

### DW-B51-03 — Extraction Sequence

To close DW-B51-03 in a future block:

1. Read `OnFollowerAtmTemplateComboLoaded` verbatim (raw=true) to confirm branch inventory
   still matches the 11-branch table above.
2. Extract `PopulateAtmComboItems` first (branches 5–7). Confirm CYC of extracted method ≤ 5.
3. Extract `ApplyAtmAutoSelect` second (branches 9–11). Confirm CYC of extracted method ≤ 4.
4. Confirm parent method CYC reduces to ≤ 5.
5. Run SCAN-01 through SCAN-07 in sequence. SCAN-06 should report parent CYC ≤ 5.
6. Run `dotnet build`. Confirm 0 errors.
7. Run `powershell -File scripts\verify_links.ps1`. Confirm DESYNC=0.

Do NOT attempt to reduce branches 1–4 (null guard, idempotency guard, contains guard, clone check)
— they are all load-bearing and cannot be removed without changing observable behavior.

### DW-B50-01 — Live F5 Protocol

See B50-LaneA/06-deferred-backlog.md §Notes for full F5 protocol steps.
Key signal: absence of `"PTT-Clone: no ATM cache -- using Inherit fallback"` in StatusUpdate log.

### DW-B50-02 — Weak Reference Implementation

See B50-LaneA/06-deferred-backlog.md §DW-B50-02 for implementation pattern.
CYC impact: `UpdateAtmComboVisibility` rises from 2 to 4 after the change (still ≤ 8).

---

## PIPELINE_COMPLETE Gate Reference

This file satisfies the `06-deferred-backlog.md` requirement for the B51 Lane A PIPELINE_COMPLETE gate.

**Gate check**:
- DW-B51-01 and DW-B51-02 are documented as CLOSED (implemented in B51).
- DW-B51-03 is documented as OPEN with rationale, branch inventory, extraction targets,
  CYC estimates, and suggested method names.
- DW-B50-01, DW-B50-02, DW-B47-05, DW-B43-02 are carried forward with no priority change.
- None of the OPEN items is a P0 blocking issue.
- PIPELINE_COMPLETE is unblocked.
