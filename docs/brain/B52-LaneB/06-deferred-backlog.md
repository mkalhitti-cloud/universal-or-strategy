# Deferred Backlog — PTT-COPIER-B52 Lane B

**Lane**: B52-LaneB (knowledge-doc-weak-refs)
**Date**: 2026-08-08

---

## Items Closed By This Lane

| Item | Title | Closed By | Evidence |
|------|-------|-----------|----------|
| **DW-B50C-02** | Document NinjaTrader.Client.dll removal: CS0433 Globals ambiguity, Core.dll replacement, Do NOT add rule | **Closed by B52-LaneB T1** | `NT8_ADDON_KNOWLEDGE.md` lines 1634–1663 — `## B52 Discoveries (2026-08-08)` section present |
| **DW-B50-02** | Replace `_atmComboRefs List<ComboBox>` with `List<WeakReference<ComboBox>>`; prune-on-iterate in `UpdateAtmComboVisibility`; `OnFollowerAtmTemplateComboLoaded` wraps with `new WeakReference<ComboBox>(cb)` | **Closed by B52-LaneB T2** | `TradeCopierPanel.cs` lines 202, 1486–1491, 1983 confirmed by independent scan |

---

## Items Carried Forward — Open (No Change)

| Item | Title | Priority | Notes |
|------|-------|----------|-------|
| **DW-B50-01** | Persistent clone ATM template selection across panel rebuilds — per-follower ATM selection state is lost when the inline ScrollViewer row is rebuilt (e.g., on `LoadFollowers()` reinvoke) | P2 | No change. Carries to B53 or later. |
| **DW-B43-02** | Click trader: true pixel-to-price mapping via NT8 scale panel API — current implementation uses `ChartPanel.MaxValue/MinValue` linear interpolation; true scale panel API not yet confirmed | P3 | No change. Shelved per Director. |
| **DW-B47-05** | Collapsible sections persistence — `_copierCollapsed` and `_isCollapsed` state is not persisted to a serializable config; collapses reset to default on NT8 restart | P3 | No change. Carries to future block. |

---

## No New Deferred Items

B52-LaneB introduced no new defects and no new deferred work items. Both tickets executed to closure within scope. The WeakReference change (T2) is a complete implementation — no follow-up items remain for the `_atmComboRefs` pattern.
